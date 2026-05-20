using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Contract creation and handler registration for requests, notifications, and stream requests,
/// including pipeline descriptor creation.
/// </summary>
internal static partial class DiscoveryModelBuilder
{
    private static bool TryCreateRequestContract(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        IAssemblySymbol primaryAssembly,
        out RawRequestContract requestContract)
    {
        requestContract = null!;

        if (!IsDiscoverableType(type))
        {
            return false;
        }

        var requestInterface = FindBestRequestInterface(type, discoverySymbols);
        if (requestInterface is null)
        {
            return false;
        }

        var responseType = requestInterface.TypeArguments[0];
        requestContract = new RawRequestContract(
            GetMetadataName(type),
            GetNamespace(type),
            type.Name,
            GetRequestKind(type, discoverySymbols),
            CreateResponseDescriptor(responseType),
            type,
            responseType,
            type.IsValueType,
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, primaryAssembly),
            type.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? type.Locations.FirstOrDefault());

        return true;
    }

    private static IEnumerable<HandlerDescriptor> CreateRequestHandlers(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        if (!IsDiscoverableType(type))
        {
            yield break;
        }

        var handlerInterfaces = type.AllInterfaces
            .Select(candidate => CreateRequestHandlerDescriptor(type, candidate, discoverySymbols, discoveryState, primaryAssembly))
            .OfType<(string Key, int Priority, HandlerDescriptor Descriptor)>()
            .GroupBy(static descriptor => descriptor.Key, StringComparer.Ordinal)
            .Select(static group => group.OrderBy(static item => item.Priority).First().Descriptor)
            .OrderBy(static descriptor => descriptor.MetadataName, StringComparer.Ordinal);

        foreach (var handlerInterface in handlerInterfaces)
        {
            yield return handlerInterface;
        }
    }

    private static (string Key, int Priority, HandlerDescriptor Descriptor)? CreateRequestHandlerDescriptor(
        INamedTypeSymbol type,
        INamedTypeSymbol candidate,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        var originalDefinition = candidate.OriginalDefinition;
        HandlerKind? handlerKind = null;
        var priority = 0;

        if (SymbolEqualityComparer.Default.Equals(originalDefinition, discoverySymbols.QueryHandlerInterface))
        {
            handlerKind = HandlerKind.Query;
            priority = 0;
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, discoverySymbols.CommandHandlerOfResponseInterface))
        {
            handlerKind = HandlerKind.CommandWithResponse;
            priority = 1;
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, discoverySymbols.CommandHandlerInterface))
        {
            handlerKind = HandlerKind.Command;
            priority = 2;
        }
        else if (SymbolEqualityComparer.Default.Equals(originalDefinition, discoverySymbols.HandlerInterface))
        {
            handlerKind = HandlerKind.Request;
            priority = 3;
        }

        if (handlerKind is null)
        {
            return null;
        }

        var requestType = candidate.TypeArguments[0];
        var responseType = handlerKind is HandlerKind.Command
            ? discoverySymbols.UnitType
            : candidate.TypeArguments[1];
        var isAccessibleToGeneratedMediator = IsAccessibleToGeneratedMediator(type, primaryAssembly);
        var hasCompatibleHandleMethod = HasCompatibleRequestHandleMethod(type, requestType, responseType, discoverySymbols, primaryAssembly);
        ReportInaccessibleRegistrationType(type, isAccessibleToGeneratedMediator, primaryAssembly, discoveryState);
        ReportInvalidHandlerSignature(type, GetTypeDisplayString(requestType), hasCompatibleHandleMethod, primaryAssembly, discoveryState);
        var descriptor = new HandlerDescriptor(
            GetMetadataName(type),
            GetTypeDisplayString(requestType),
            GetTypeDisplayString(responseType),
            GetHandleMethodName(type),
            type.DeclaredAccessibility,
            isAccessibleToGeneratedMediator,
            hasCompatibleHandleMethod,
            handlerKind.Value);

        if (isAccessibleToGeneratedMediator && hasCompatibleHandleMethod)
        {
            ReportDuplicateGeneratedRegistration(
                type,
                MediatorGenerationNames.GetSelfRegistrationServiceType(descriptor.MetadataName),
                descriptor.MetadataName,
                primaryAssembly,
                discoveryState);
            ReportDuplicateGeneratedRegistration(
                type,
                MediatorGenerationNames.GetHandlerServiceType(descriptor),
                descriptor.MetadataName,
                primaryAssembly,
                discoveryState);
        }

        return ($"{descriptor.RequestMetadataName}|{descriptor.ResponseMetadataName}", priority, descriptor);
    }

    private static IEnumerable<RawPipelineDescriptor> CreatePipelines(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        if (!IsDiscoverableType(type))
        {
            yield break;
        }

        var isAccessibleToGeneratedMediator = IsAccessibleToGeneratedMediator(type, primaryAssembly);
        ReportInaccessibleRegistrationType(type, isAccessibleToGeneratedMediator, primaryAssembly, discoveryState);

        var pipelineTypeArguments = type.AllInterfaces
            .Where(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.PipelineInterface))
            .Select(static candidate => candidate.TypeArguments);

        foreach (var typeArguments in pipelineTypeArguments)
        {
            var pipelineOrder = type.GetAttributes().SingleOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    discoverySymbols.PipelineOrderAttribute));

            var stage = pipelineOrder?.ConstructorArguments[0].Value is int stageValue ? stageValue : 0;
            var order = pipelineOrder?.NamedArguments.FirstOrDefault(
                static argument => argument.Key == "Order").Value.Value is int orderValue
                ? orderValue
                : 0;

            yield return new RawPipelineDescriptor(
                GetMetadataName(type),
                type.TypeParameters.Length > 0 ? GetMetadataName(type.OriginalDefinition) : null,
                stage,
                order,
                type.TypeParameters.Length > 0 ? PipelineApplicability.OpenGeneric : PipelineApplicability.Closed,
                isAccessibleToGeneratedMediator,
                IsStream: false,
                GetTypeDisplayString(typeArguments[0]),
                GetTypeDisplayString(typeArguments[1]),
                type,
                typeArguments[0],
                typeArguments[1],
                [.. type.TypeParameters]);

            if (isAccessibleToGeneratedMediator)
            {
                var implementationType = GetMetadataName(type);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetSelfRegistrationServiceType(implementationType),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetPipelineServiceType(GetTypeDisplayString(typeArguments[0]), GetTypeDisplayString(typeArguments[1])),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
            }
        }
    }

    private static bool TryCreateNotificationContract(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        out RawNotificationContract notificationContract)
    {
        notificationContract = null!;

        if (!IsDiscoverableType(type) || !ImplementsInterface(type, discoverySymbols.NotificationInterface))
        {
            return false;
        }

        var notificationDispatchAttribute = discoverySymbols.NotificationDispatchAttribute is null
            ? null
            : type.GetAttributes().SingleOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    discoverySymbols.NotificationDispatchAttribute));
        var publishInParallel = notificationDispatchAttribute?.ConstructorArguments[0].Value is int strategy
                                && strategy == ParallelNotificationDispatchStrategyValue;

        notificationContract = new RawNotificationContract(GetMetadataName(type), type.ContainingAssembly.Name, publishInParallel);
        return true;
    }

    private static IEnumerable<RawNotificationHandlerDescriptor> CreateNotificationHandlers(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        if (!IsDiscoverableType(type))
        {
            yield break;
        }

        var isAccessibleToGeneratedMediator = IsAccessibleToGeneratedMediator(type, primaryAssembly);
        ReportInaccessibleRegistrationType(type, isAccessibleToGeneratedMediator, primaryAssembly, discoveryState);

        var notificationTypeArguments = type.AllInterfaces
            .Where(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.NotificationHandlerInterface))
            .Select(static candidate => candidate.TypeArguments);

        foreach (var typeArguments in notificationTypeArguments)
        {
            var notificationOrder = type.GetAttributes().SingleOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    discoverySymbols.NotificationOrderAttribute));
            var hasExplicitOrder = notificationOrder is not null;
            var order = notificationOrder?.ConstructorArguments[0].Value is int orderValue ? orderValue : 0;

            yield return new RawNotificationHandlerDescriptor(
                GetMetadataName(type),
                GetNamespace(type),
                type.Name,
                GetTypeDisplayString(typeArguments[0]),
                GetHandleMethodName(type),
                type.DeclaredAccessibility,
                isAccessibleToGeneratedMediator,
                order,
                hasExplicitOrder,
                GetDiagnosticLocation(type, primaryAssembly));

            if (isAccessibleToGeneratedMediator)
            {
                var implementationType = GetMetadataName(type);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetSelfRegistrationServiceType(implementationType),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetNotificationHandlerServiceType(GetTypeDisplayString(typeArguments[0])),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
            }
        }
    }

    private static bool TryCreateStreamRequestContract(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        IAssemblySymbol primaryAssembly,
        out RawStreamRequestContract streamRequestContract)
    {
        streamRequestContract = null!;

        if (!IsDiscoverableType(type))
        {
            return false;
        }

        var streamRequestInterface = type.AllInterfaces.SingleOrDefault(
            candidate => SymbolEqualityComparer.Default.Equals(
                candidate.OriginalDefinition,
                discoverySymbols.StreamRequestInterface));

        if (streamRequestInterface is null)
        {
            return false;
        }

        var itemResponseType = streamRequestInterface.TypeArguments[0];
        streamRequestContract = new RawStreamRequestContract(
            GetMetadataName(type),
            CreateResponseDescriptor(itemResponseType),
            type,
            itemResponseType,
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, primaryAssembly),
            type.Locations.FirstOrDefault(static candidate => candidate.IsInSource) ?? type.Locations.FirstOrDefault());
        return true;
    }

    private static IEnumerable<StreamHandlerDescriptor> CreateStreamHandlers(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        if (!IsDiscoverableType(type))
        {
            yield break;
        }

        var isAccessibleToGeneratedMediator = IsAccessibleToGeneratedMediator(type, primaryAssembly);
        ReportInaccessibleRegistrationType(type, isAccessibleToGeneratedMediator, primaryAssembly, discoveryState);

        var streamHandlerTypeArguments = type.AllInterfaces
            .Where(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.StreamHandlerInterface))
            .Select(static candidate => candidate.TypeArguments);

        foreach (var typeArguments in streamHandlerTypeArguments)
        {
            var requestType = typeArguments[0];
            var responseType = typeArguments[1];
            var hasCompatibleHandleMethod = HasCompatibleStreamHandleMethod(type, requestType, responseType, discoverySymbols, primaryAssembly);
            ReportInvalidHandlerSignature(type, GetTypeDisplayString(requestType), hasCompatibleHandleMethod, primaryAssembly, discoveryState);

            yield return new StreamHandlerDescriptor(
                GetMetadataName(type),
                GetTypeDisplayString(typeArguments[0]),
                GetHandleMethodName(type),
                type.DeclaredAccessibility,
                isAccessibleToGeneratedMediator,
                hasCompatibleHandleMethod);

            if (isAccessibleToGeneratedMediator && hasCompatibleHandleMethod)
            {
                var implementationType = GetMetadataName(type);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetSelfRegistrationServiceType(implementationType),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetStreamHandlerServiceType(GetTypeDisplayString(typeArguments[0]), GetTypeDisplayString(typeArguments[1])),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
            }
        }
    }

    private static IEnumerable<RawPipelineDescriptor> CreateStreamPipelines(
        INamedTypeSymbol type,
        DiscoverySymbols discoverySymbols,
        DiscoveryState discoveryState,
        IAssemblySymbol primaryAssembly)
    {
        if (!IsDiscoverableType(type))
        {
            yield break;
        }

        var isAccessibleToGeneratedMediator = IsAccessibleToGeneratedMediator(type, primaryAssembly);
        ReportInaccessibleRegistrationType(type, isAccessibleToGeneratedMediator, primaryAssembly, discoveryState);

        var pipelineTypeArguments = type.AllInterfaces
            .Where(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    discoverySymbols.StreamPipelineInterface))
            .Select(static candidate => candidate.TypeArguments);

        foreach (var typeArguments in pipelineTypeArguments)
        {
            var pipelineOrder = type.GetAttributes().SingleOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(
                    attribute.AttributeClass,
                    discoverySymbols.PipelineOrderAttribute));

            var stage = pipelineOrder?.ConstructorArguments[0].Value is int stageValue ? stageValue : 0;
            var order = pipelineOrder?.NamedArguments.FirstOrDefault(
                static argument => argument.Key == "Order").Value.Value is int orderValue
                ? orderValue
                : 0;

            yield return new RawPipelineDescriptor(
                GetMetadataName(type),
                type.TypeParameters.Length > 0 ? GetMetadataName(type.OriginalDefinition) : null,
                stage,
                order,
                type.TypeParameters.Length > 0 ? PipelineApplicability.OpenGeneric : PipelineApplicability.Closed,
                isAccessibleToGeneratedMediator,
                IsStream: true,
                GetTypeDisplayString(typeArguments[0]),
                GetTypeDisplayString(typeArguments[1]),
                type,
                typeArguments[0],
                typeArguments[1],
                [.. type.TypeParameters]);

            if (isAccessibleToGeneratedMediator)
            {
                var implementationType = GetMetadataName(type);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetSelfRegistrationServiceType(implementationType),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
                ReportDuplicateGeneratedRegistration(
                    type,
                    MediatorGenerationNames.GetStreamPipelineServiceType(GetTypeDisplayString(typeArguments[0]), GetTypeDisplayString(typeArguments[1])),
                    implementationType,
                    primaryAssembly,
                    discoveryState);
            }
        }
    }
}
