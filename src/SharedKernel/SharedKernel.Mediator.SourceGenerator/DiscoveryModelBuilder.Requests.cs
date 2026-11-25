using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Request, notification, and stream request descriptor construction,
/// including pipeline matching and open-generic type binding.
/// </summary>
internal static partial class DiscoveryModelBuilder
{
    private static ImmutableArray<RequestDescriptor> BuildRequestDescriptors(
        IDictionary<string, RawRequestContract> requestContracts,
        IEnumerable<HandlerDescriptor> requestHandlers,
        IEnumerable<RawPipelineDescriptor> pipelines,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        var handlersByRequest = requestHandlers
            .GroupBy(static handler => handler.RequestMetadataName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static handler => handler.MetadataName, StringComparer.Ordinal)
                    .ThenBy(static handler => handler.Kind)
                    .ToImmutableArray(),
                StringComparer.Ordinal);

        var matchedPipelinesByRequest = requestContracts.Keys.ToDictionary(
            static requestMetadataName => requestMetadataName,
            static _ => new List<(PipelineDescriptor Descriptor, RawPipelineDescriptor Raw)>(),
            StringComparer.Ordinal);

        foreach (var pipeline in pipelines)
        {
            foreach (var match in MatchPipelineDescriptors(pipeline, requestContracts.Values, primaryAssembly, discoveryState))
            {
                if (matchedPipelinesByRequest.TryGetValue(match.Descriptor.RequestMetadataName, out var requestPipelines))
                {
                    requestPipelines.Add(match);
                }
            }
        }

        foreach (var requestPipelines in matchedPipelinesByRequest)
        {
            foreach (var duplicateGroup in requestPipelines.Value
                         .GroupBy(static pipeline => (pipeline.Descriptor.Stage, pipeline.Descriptor.Order))
                         .Where(static group => group.Count() > 1))
            {
                foreach (var pipeline in duplicateGroup)
                {
                    discoveryState.Diagnostics.Add(
                        Diagnostic.Create(
                            MediatorDiagnosticDescriptors.DuplicatePipelineOrder,
                            GetDiagnosticLocation(pipeline.Raw.TypeSymbol, primaryAssembly),
                            requestPipelines.Key,
                            duplicateGroup.Key.Stage,
                            duplicateGroup.Key.Order));
                }
            }
        }

        return [.. requestContracts.Values
            .OrderBy(static request => request.MetadataName, StringComparer.Ordinal)
            .Select(
                request =>
                {
                    var handlers = handlersByRequest.TryGetValue(request.MetadataName, out var requestHandlers)
                        ? requestHandlers
                        : [];
                    ImmutableArray<PipelineDescriptor> requestPipelines = matchedPipelinesByRequest.TryGetValue(
                        request.MetadataName,
                        out var discoveredPipelines)
                        ? [.. discoveredPipelines
                            .OrderBy(static pipeline => pipeline.Descriptor.Stage)
                            .ThenBy(static pipeline => pipeline.Descriptor.Order)
                            .ThenBy(static pipeline => pipeline.Descriptor.MetadataName, StringComparer.Ordinal)
                            .Select(static pipeline => pipeline.Descriptor)]
                        : [];

                    return new RequestDescriptor(
                        request.MetadataName,
                        request.Name,
                        request.TypeSymbol.ContainingAssembly.Name,
                        request.Kind,
                        request.Response,
                        request.IsValueType,
                        handlers,
                        requestPipelines);
                })];
    }

    private static IEnumerable<(PipelineDescriptor Descriptor, RawPipelineDescriptor Raw)> MatchPipelineDescriptors(
        RawPipelineDescriptor pipeline,
        IEnumerable<RawRequestContract> requestContracts,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        if (!pipeline.IsAccessibleToGeneratedMediator)
        {
            yield break;
        }

        if (pipeline.Applicability == PipelineApplicability.Closed)
        {
            var matchedRequest = requestContracts.FirstOrDefault(
                request =>
                    string.Equals(request.MetadataName, pipeline.RequestMetadataName, StringComparison.Ordinal)
                    && string.Equals(request.Response.MetadataName, pipeline.ResponseMetadataName, StringComparison.Ordinal));

            if (matchedRequest is null)
            {
                ReportNeverAppliesPipeline(pipeline, primaryAssembly, discoveryState);
                yield break;
            }

            yield return (
                new PipelineDescriptor(
                    pipeline.MetadataName,
                    pipeline.OpenGenericMetadataName,
                    matchedRequest.MetadataName,
                    pipeline.Stage,
                    pipeline.Order,
                    PipelineApplicability.Closed,
                    pipeline.IsAccessibleToGeneratedMediator,
                    pipeline.IsStream),
                pipeline);
            yield break;
        }

        if (pipeline.TypeParameters.Length != 2)
        {
            ReportInvalidPipelineGenericArity(pipeline, primaryAssembly, discoveryState);
            yield break;
        }

        var matchedAny = false;
        var constraintFailure = false;

        foreach (var request in requestContracts)
        {
            if (TryBindOpenGenericPipeline(pipeline, request, out var descriptor, out var failedConstraints))
            {
                matchedAny = true;
                yield return (descriptor, pipeline);
                continue;
            }

            constraintFailure |= failedConstraints;
        }

        if (matchedAny)
        {
            yield break;
        }

        if (constraintFailure)
        {
            ReportUnboundPipelineConstraints(pipeline, primaryAssembly, discoveryState);
            yield break;
        }

        ReportNeverAppliesPipeline(pipeline, primaryAssembly, discoveryState);
    }

    private static bool TryBindOpenGenericPipeline(
        RawPipelineDescriptor pipeline,
        RawRequestContract request,
        out PipelineDescriptor descriptor,
        out bool failedConstraints)
    {
        descriptor = null!;
        failedConstraints = false;

        var bindings = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        if (!TryUnifyTypePattern(pipeline.RequestTypePattern, request.TypeSymbol, bindings)
            || !TryUnifyTypePattern(pipeline.ResponseTypePattern, request.ResponseTypeSymbol, bindings))
        {
            return false;
        }

        if (pipeline.TypeParameters.Any(typeParameter => !bindings.ContainsKey(typeParameter))
            || !TypeArgumentsSatisfyConstraints(pipeline.TypeParameters, bindings))
        {
            failedConstraints = true;
            return false;
        }

        var constructedType = pipeline.TypeSymbol.OriginalDefinition.Construct(
            [.. pipeline.TypeParameters.Select(typeParameter => bindings[typeParameter])]);
        descriptor = new PipelineDescriptor(
            GetMetadataName(constructedType),
            pipeline.OpenGenericMetadataName,
            request.MetadataName,
            pipeline.Stage,
            pipeline.Order,
            PipelineApplicability.OpenGeneric,
            pipeline.IsAccessibleToGeneratedMediator,
            pipeline.IsStream);
        return true;
    }

    private static bool TryUnifyTypePattern(
        ITypeSymbol pattern,
        ITypeSymbol actual,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (pattern is ITypeParameterSymbol typeParameter)
        {
            if (bindings.TryGetValue(typeParameter, out var boundType))
            {
                return SymbolEqualityComparer.Default.Equals(boundType, actual);
            }

            bindings[typeParameter] = actual;
            return true;
        }

        if (pattern is INamedTypeSymbol namedPattern)
        {
            if (actual is not INamedTypeSymbol namedActual
                || !SymbolEqualityComparer.Default.Equals(namedPattern.OriginalDefinition, namedActual.OriginalDefinition)
                || namedPattern.TypeArguments.Length != namedActual.TypeArguments.Length)
            {
                return false;
            }

            for (var index = 0; index < namedPattern.TypeArguments.Length; index++)
            {
                if (!TryUnifyTypePattern(namedPattern.TypeArguments[index], namedActual.TypeArguments[index], bindings))
                {
                    return false;
                }
            }

            return true;
        }

        return SymbolEqualityComparer.Default.Equals(pattern, actual);
    }

    private static bool TypeArgumentsSatisfyConstraints(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        foreach (var typeParameter in typeParameters)
        {
            var actualType = bindings[typeParameter];
            if (!TypeArgumentSatisfiesConstraints(actualType, typeParameter, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TypeArgumentSatisfiesConstraints(
        ITypeSymbol actualType,
        ITypeParameterSymbol typeParameter,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (typeParameter.HasReferenceTypeConstraint && !actualType.IsReferenceType)
        {
            return false;
        }

        if (typeParameter.HasValueTypeConstraint && !actualType.IsValueType)
        {
            return false;
        }

        if (typeParameter.HasUnmanagedTypeConstraint && !actualType.IsUnmanagedType)
        {
            return false;
        }

        if (typeParameter.HasNotNullConstraint && actualType.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return false;
        }

        if (typeParameter.HasConstructorConstraint
            && actualType is INamedTypeSymbol namedActual
            && !namedActual.IsValueType
            && !namedActual.InstanceConstructors.Any(static constructor => constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public))
        {
            return false;
        }

        foreach (var constraintType in typeParameter.ConstraintTypes)
        {
            var substitutedConstraint = SubstituteTypeParameters(constraintType, bindings);
            if (substitutedConstraint is null || !ImplementsOrDerivesFrom(actualType, substitutedConstraint))
            {
                return false;
            }
        }

        return true;
    }

    private static ITypeSymbol? SubstituteTypeParameters(
        ITypeSymbol type,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return bindings.TryGetValue(typeParameter, out var boundType) ? boundType : null;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var substitutedArguments = new ITypeSymbol[namedType.TypeArguments.Length];
            for (var index = 0; index < namedType.TypeArguments.Length; index++)
            {
                var substitutedArgument = SubstituteTypeParameters(namedType.TypeArguments[index], bindings);
                if (substitutedArgument is null)
                {
                    return null;
                }

                substitutedArguments[index] = substitutedArgument;
            }

            return namedType.OriginalDefinition.Construct(substitutedArguments);
        }

        return type;
    }

    private static bool ImplementsOrDerivesFrom(ITypeSymbol actualType, ITypeSymbol constraintType)
    {
        if (SymbolEqualityComparer.Default.Equals(actualType, constraintType))
        {
            return true;
        }

        if (actualType is INamedTypeSymbol namedActual)
        {
            if (namedActual.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, constraintType)))
            {
                return true;
            }

            for (var baseType = namedActual.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, constraintType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ReportInvalidPipelineGenericArity(
        RawPipelineDescriptor pipeline,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.InvalidPipelineGenericArity,
                GetDiagnosticLocation(pipeline.TypeSymbol, primaryAssembly),
                pipeline.MetadataName,
                pipeline.TypeParameters.Length));
    }

    private static void ReportNeverAppliesPipeline(
        RawPipelineDescriptor pipeline,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.NeverAppliesPipeline,
                GetDiagnosticLocation(pipeline.TypeSymbol, primaryAssembly),
                pipeline.MetadataName));
    }

    private static void ReportUnboundPipelineConstraints(
        RawPipelineDescriptor pipeline,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.UnboundPipelineConstraints,
                GetDiagnosticLocation(pipeline.TypeSymbol, primaryAssembly),
                pipeline.MetadataName));
    }

    private static void ReportNotificationHandlersRequireExplicitOrder(
        string notificationMetadataName,
        RawNotificationHandlerDescriptor handler,
        DiscoveryState discoveryState)
    {
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.NotificationHandlersRequireExplicitOrder,
                handler.DiagnosticLocation ?? Location.None,
                notificationMetadataName,
                handler.MetadataName));
    }

    private static void ReportDuplicateNotificationHandlerOrder(
        string notificationMetadataName,
        RawNotificationHandlerDescriptor handler,
        int order,
        DiscoveryState discoveryState)
    {
        discoveryState.Diagnostics.Add(
            Diagnostic.Create(
                MediatorDiagnosticDescriptors.DuplicateNotificationHandlerOrder,
                handler.DiagnosticLocation ?? Location.None,
                notificationMetadataName,
                order));
    }

    private static ImmutableArray<NotificationDescriptor> BuildNotificationDescriptors(
        IDictionary<string, RawNotificationContract> notificationContracts,
        IEnumerable<RawNotificationHandlerDescriptor> notificationHandlers,
        DiscoveryState discoveryState)
    {
        var handlersByNotification = notificationHandlers
            .GroupBy(static handler => handler.NotificationMetadataName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group =>
                {
                    var publishInParallel = notificationContracts.TryGetValue(group.Key, out var notificationContract)
                        && notificationContract.PublishInParallel;
                    return OrderNotificationHandlers(
                        group.Key,
                        [.. group],
                        publishInParallel,
                        discoveryState);
                },
                StringComparer.Ordinal);

        return [.. notificationContracts.Values
            .OrderBy(static notification => notification.MetadataName, StringComparer.Ordinal)
            .Select(
                notification =>
                {
                    var handlers = handlersByNotification.TryGetValue(notification.MetadataName, out var notificationHandlersForContract)
                        ? notificationHandlersForContract
                        : [];

                    var lastDotIndex = notification.MetadataName.LastIndexOf('.');
                    var name = lastDotIndex >= 0
                        ? notification.MetadataName.Substring(lastDotIndex + 1)
                        : notification.MetadataName;

                    return new NotificationDescriptor(
                        notification.MetadataName,
                        name,
                        notification.AssemblyName,
                        notification.PublishInParallel,
                        handlers);
                })];
    }

    private static ImmutableArray<NotificationHandlerDescriptor> OrderNotificationHandlers(
        string notificationMetadataName,
        ImmutableArray<RawNotificationHandlerDescriptor> handlers,
        bool publishInParallel,
        DiscoveryState discoveryState)
    {
        var accessibleHandlers = handlers
            .Where(static handler => handler.IsAccessibleToGeneratedMediator)
            .ToImmutableArray();

        if (!publishInParallel && accessibleHandlers.Length > 1)
        {
            foreach (var unorderedHandler in accessibleHandlers
                         .Where(static handler => !handler.HasExplicitOrder)
                         .OrderBy(static handler => handler.MetadataName, StringComparer.Ordinal))
            {
                ReportNotificationHandlersRequireExplicitOrder(notificationMetadataName, unorderedHandler, discoveryState);
            }
        }

        if (!publishInParallel)
        {
            foreach (var duplicateGroup in accessibleHandlers
                         .Where(static handler => handler.HasExplicitOrder)
                         .GroupBy(static handler => handler.Order)
                         .Where(static group => group.Count() > 1))
            {
                foreach (var handler in duplicateGroup)
                {
                    ReportDuplicateNotificationHandlerOrder(notificationMetadataName, handler, duplicateGroup.Key, discoveryState);
                }
            }
        }

        return [.. handlers
            .OrderByDescending(handler => !publishInParallel && handler.HasExplicitOrder)
            .ThenBy(handler => publishInParallel ? 0 : handler.Order)
            .ThenBy(static handler => handler.MetadataName, StringComparer.Ordinal)
            .Select(
                static handler => new NotificationHandlerDescriptor(
                    handler.MetadataName,
                    handler.MethodName,
                    handler.Accessibility,
                    handler.IsAccessibleToGeneratedMediator))];
    }

    private static ImmutableArray<StreamRequestDescriptor> BuildStreamRequestDescriptors(
        IDictionary<string, RawStreamRequestContract> streamRequestContracts,
        IEnumerable<StreamHandlerDescriptor> streamHandlers,
        IEnumerable<RawPipelineDescriptor> streamPipelines,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        var handlersByRequest = streamHandlers
            .GroupBy(static handler => handler.RequestMetadataName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static handler => handler.MetadataName, StringComparer.Ordinal)
                    .ToImmutableArray(),
                StringComparer.Ordinal);

        var matchedPipelinesByRequest = streamRequestContracts.Keys.ToDictionary(
            static requestMetadataName => requestMetadataName,
            static _ => new List<(PipelineDescriptor Descriptor, RawPipelineDescriptor Raw)>(),
            StringComparer.Ordinal);

        foreach (var pipeline in streamPipelines)
        {
            foreach (var match in MatchStreamPipelineDescriptors(pipeline, streamRequestContracts.Values, primaryAssembly, discoveryState))
            {
                if (matchedPipelinesByRequest.TryGetValue(match.Descriptor.RequestMetadataName, out var requestPipelines))
                {
                    requestPipelines.Add(match);
                }
            }
        }

        foreach (var requestPipelines in matchedPipelinesByRequest)
        {
            foreach (var duplicateGroup in requestPipelines.Value
                         .GroupBy(static pipeline => (pipeline.Descriptor.Stage, pipeline.Descriptor.Order))
                         .Where(static group => group.Count() > 1))
            {
                foreach (var pipeline in duplicateGroup)
                {
                    discoveryState.Diagnostics.Add(
                        Diagnostic.Create(
                            MediatorDiagnosticDescriptors.DuplicatePipelineOrder,
                            GetDiagnosticLocation(pipeline.Raw.TypeSymbol, primaryAssembly),
                            requestPipelines.Key,
                            duplicateGroup.Key.Stage,
                            duplicateGroup.Key.Order));
                }
            }
        }

        return [.. streamRequestContracts.Values
            .OrderBy(static request => request.MetadataName, StringComparer.Ordinal)
            .Select(
                request =>
                {
                    var handlers = handlersByRequest.TryGetValue(request.MetadataName, out var streamHandlersForRequest)
                        ? streamHandlersForRequest
                        : [];
                    ImmutableArray<PipelineDescriptor> requestPipelines = matchedPipelinesByRequest.TryGetValue(
                        request.MetadataName,
                        out var discoveredPipelines)
                        ? [.. discoveredPipelines
                            .OrderBy(static pipeline => pipeline.Descriptor.Stage)
                            .ThenBy(static pipeline => pipeline.Descriptor.Order)
                            .ThenBy(static pipeline => pipeline.Descriptor.MetadataName, StringComparer.Ordinal)
                            .Select(static pipeline => pipeline.Descriptor)]
                        : [];

                    return new StreamRequestDescriptor(
                        request.MetadataName,
                        request.TypeSymbol.Name,
                        request.TypeSymbol.ContainingAssembly.Name,
                        request.ItemResponse,
                        handlers,
                        requestPipelines);
                })];
    }

    private static IEnumerable<(PipelineDescriptor Descriptor, RawPipelineDescriptor Raw)> MatchStreamPipelineDescriptors(
        RawPipelineDescriptor pipeline,
        IEnumerable<RawStreamRequestContract> streamRequestContracts,
        IAssemblySymbol primaryAssembly,
        DiscoveryState discoveryState)
    {
        if (!pipeline.IsAccessibleToGeneratedMediator)
        {
            yield break;
        }

        if (pipeline.Applicability == PipelineApplicability.Closed)
        {
            var matchedRequest = streamRequestContracts.FirstOrDefault(
                request =>
                    string.Equals(request.MetadataName, pipeline.RequestMetadataName, StringComparison.Ordinal)
                    && string.Equals(request.ItemResponse.MetadataName, pipeline.ResponseMetadataName, StringComparison.Ordinal));

            if (matchedRequest is null)
            {
                ReportNeverAppliesPipeline(pipeline, primaryAssembly, discoveryState);
                yield break;
            }

            yield return (
                new PipelineDescriptor(
                    pipeline.MetadataName,
                    pipeline.OpenGenericMetadataName,
                    matchedRequest.MetadataName,
                    pipeline.Stage,
                    pipeline.Order,
                    PipelineApplicability.Closed,
                    pipeline.IsAccessibleToGeneratedMediator,
                    pipeline.IsStream),
                pipeline);
            yield break;
        }

        if (pipeline.TypeParameters.Length != 2)
        {
            ReportInvalidPipelineGenericArity(pipeline, primaryAssembly, discoveryState);
            yield break;
        }

        var matchedAny = false;
        var constraintFailure = false;

        foreach (var request in streamRequestContracts)
        {
            if (TryBindOpenGenericStreamPipeline(pipeline, request, out var descriptor, out var failedConstraints))
            {
                matchedAny = true;
                yield return (descriptor, pipeline);
                continue;
            }

            constraintFailure |= failedConstraints;
        }

        if (matchedAny)
        {
            yield break;
        }

        if (constraintFailure)
        {
            ReportUnboundPipelineConstraints(pipeline, primaryAssembly, discoveryState);
            yield break;
        }

        ReportNeverAppliesPipeline(pipeline, primaryAssembly, discoveryState);
    }

    private static bool TryBindOpenGenericStreamPipeline(
        RawPipelineDescriptor pipeline,
        RawStreamRequestContract request,
        out PipelineDescriptor descriptor,
        out bool failedConstraints)
    {
        descriptor = null!;
        failedConstraints = false;

        var bindings = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        if (!TryUnifyTypePattern(pipeline.RequestTypePattern, request.TypeSymbol, bindings)
            || !TryUnifyTypePattern(pipeline.ResponseTypePattern, request.ItemResponseTypeSymbol, bindings))
        {
            return false;
        }

        if (pipeline.TypeParameters.Any(typeParameter => !bindings.ContainsKey(typeParameter))
            || !TypeArgumentsSatisfyConstraints(pipeline.TypeParameters, bindings))
        {
            failedConstraints = true;
            return false;
        }

        var constructedType = pipeline.TypeSymbol.OriginalDefinition.Construct(
            [.. pipeline.TypeParameters.Select(typeParameter => bindings[typeParameter])]);
        descriptor = new PipelineDescriptor(
            GetMetadataName(constructedType),
            pipeline.OpenGenericMetadataName,
            request.MetadataName,
            pipeline.Stage,
            pipeline.Order,
            PipelineApplicability.OpenGeneric,
            pipeline.IsAccessibleToGeneratedMediator,
            pipeline.IsStream);
        return true;
    }
}
