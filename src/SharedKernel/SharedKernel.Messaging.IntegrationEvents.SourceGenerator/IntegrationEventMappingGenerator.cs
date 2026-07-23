using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

/// <summary>
/// Generates direct domain-to-outbox mapping and closed integration-event host dispatch.
/// </summary>
[Generator]
public sealed class IntegrationEventMappingGenerator : IIncrementalGenerator
{
    private const string AttributeName = "SharedKernel.Messaging.IntegrationEvents.IntegrationEventMappingAttribute";
    private const string DomainEventInterfaceName = "SharedKernel.Domain.IDomainEvent";
    private const string IntegrationEventInterfaceName = "SharedKernel.Messaging.IntegrationEvents.IIntegrationEvent";
    private const string IntegrationEventHandlerInterfaceName = "SharedKernel.Messaging.IntegrationEvents.IIntegrationEventHandler<TIntegrationEvent>";
    private const string RegistrationExtensionsTypeName = "SharedKernel.Messaging.IntegrationEvents.IntegrationEventConsumerServiceCollectionExtensions";
    private const string GeneratedHintName = "SharedKernel.Messaging.IntegrationEvents.GeneratedIntegrationEvents.g.cs";

    private static readonly DiagnosticDescriptor MissingConsumerHandler = new(
        "SKMSG001",
        "Integration-event consumer handler is missing",
        "Integration-event consumer '{0}' must have one concrete IIntegrationEventHandler<T> implementation",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateConsumerHandlers = new(
        "SKMSG002",
        "Integration-event consumer has multiple handlers",
        "Integration-event consumer '{0}' has {1} concrete IIntegrationEventHandler<T> implementations",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateConsumerEventTypes = new(
        "SKMSG003",
        "Integration-event consumers declare the same event type",
        "Integration-event consumers '{0}' declare duplicate event type '{1}'",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidIntegrationEventMapping = new(
        "SKMSG004",
        "Integration-event mapping method is invalid",
        "Method '{0}' marked with IntegrationEventMappingAttribute must be a non-generic static accessible method " +
        "on accessible non-generic containing types with signature TIntegrationEvent Method(TDomainEvent, Guid, DateTimeOffset); " +
        "TDomainEvent and TIntegrationEvent must be concrete, closed, accessible classes or structs",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateIntegrationEventMappings = new(
        "SKMSG005",
        "Integration-event mapping pair is ambiguous",
        "Domain event '{0}' has {2} mappings to integration event '{1}': {3}",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidIntegrationEventContract = new(
        "SKMSG006",
        "Registered integration-event contract is invalid",
        "Registered integration-event contract '{0}' must be a concrete, closed class or struct accessible to generated code",
        "SharedKernel.Messaging",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mappingCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is MethodDeclarationSyntax,
                static (attributeContext, cancellationToken) => BuildMapping(attributeContext, cancellationToken))
            .Collect()
            .Select(static (items, _) => items
                .OrderBy(static item => item.Mapping?.DomainEventType ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static item => item.Mapping?.IntegrationEventType ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static item => item.Mapping?.ContainingType ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static item => item.MethodDisplayName, StringComparer.Ordinal)
                .ToImmutableArray());

        var registrationCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntaxContext, cancellationToken) => BuildRegistration(syntaxContext, cancellationToken))
            .Where(static candidate => candidate is not null)
            .Collect()
            .Select(static (items, _) => items
                .Where(static item => item is not null)
                .Select(static item => item!)
                .OrderBy(static item => item.IntegrationEventType, StringComparer.Ordinal)
                .ThenBy(static item => item.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static item => item.Location.SourceSpan.Start)
                .ToImmutableArray());

        var handlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) => BuildHandlers(syntaxContext, cancellationToken))
            .Where(static discoveredHandlers => !discoveredHandlers.IsDefaultOrEmpty)
            .Collect()
            .Select(static (items, _) => items
                .SelectMany(static item => item)
                .Distinct()
                .OrderBy(static item => item.IntegrationEventType, StringComparer.Ordinal)
                .ThenBy(static item => item.HandlerType, StringComparer.Ordinal)
                .ToImmutableArray());

        var generation = mappingCandidates.Combine(registrationCandidates).Combine(handlers);
        context.RegisterSourceOutput(generation, static (productionContext, input) =>
        {
            var mappingCandidates = input.Left.Left;
            var registrationCandidates = input.Left.Right;
            var handlers = input.Right;
            foreach (var invalidMapping in mappingCandidates.Where(static candidate => candidate.Mapping is null))
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    InvalidIntegrationEventMapping,
                    invalidMapping.Location,
                    invalidMapping.MethodDisplayName));
            }

            var validMappingCandidates = mappingCandidates
                .Where(static candidate => candidate.Mapping is not null)
                .Select(static candidate => (Mapping: candidate.Mapping!, candidate.MethodDisplayName, candidate.Location))
                .ToArray();
            var duplicateMappingGroups = validMappingCandidates
                .GroupBy(static candidate => (
                    DomainEventType: candidate.Mapping.DomainEventType,
                    IntegrationEventType: candidate.Mapping.IntegrationEventType))
                .Where(static group => group.Count() > 1)
                .OrderBy(static group => group.Key.DomainEventType, StringComparer.Ordinal)
                .ThenBy(static group => group.Key.IntegrationEventType, StringComparer.Ordinal)
                .ToArray();
            foreach (var duplicateMappingGroup in duplicateMappingGroups)
            {
                var mappingMethods = duplicateMappingGroup
                    .Select(static candidate => candidate.MethodDisplayName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static method => method, StringComparer.Ordinal);
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateIntegrationEventMappings,
                    duplicateMappingGroup.First().Location,
                    duplicateMappingGroup.Key.DomainEventType,
                    duplicateMappingGroup.Key.IntegrationEventType,
                    duplicateMappingGroup.Count(),
                    string.Join(", ", mappingMethods)));
            }

            var invalidContractTypes = registrationCandidates
                .Where(static candidate => candidate.Registration is null)
                .Select(static candidate => candidate.IntegrationEventType)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var invalidRegistration in registrationCandidates.Where(static candidate => candidate.Registration is null))
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    InvalidIntegrationEventContract,
                    invalidRegistration.Location,
                    invalidRegistration.IntegrationEventType));
            }

            var duplicateMappingPairs = duplicateMappingGroups
                .Select(static group => group.Key)
                .ToArray();
            var mappings = validMappingCandidates
                .Where(candidate => !duplicateMappingPairs.Any(pair =>
                    string.Equals(pair.DomainEventType, candidate.Mapping.DomainEventType, StringComparison.Ordinal) &&
                    string.Equals(pair.IntegrationEventType, candidate.Mapping.IntegrationEventType, StringComparison.Ordinal)))
                .Select(static candidate => candidate.Mapping)
                .Where(mapping => !invalidContractTypes.Contains(mapping.IntegrationEventType, StringComparer.Ordinal))
                .ToImmutableArray();
            var registrations = registrationCandidates
                .Where(static candidate => candidate.Registration is not null)
                .Select(static candidate => candidate.Registration!)
                .Distinct()
                .OrderBy(static registration => registration.IntegrationEventType, StringComparer.Ordinal)
                .ThenBy(static registration => registration.IsConsumer)
                .ToImmutableArray();
            var consumerTypes = registrations
                .Where(static registration => registration.IsConsumer)
                .Select(static registration => registration.IntegrationEventType)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            ReportConsumerHandlerDiagnostics(productionContext, consumerTypes, handlers);

            var duplicateEventTypeGroups = registrations
                .Where(static registration => registration.IsConsumer && registration.EventType is not null)
                .GroupBy(static registration => registration.EventType!, StringComparer.Ordinal)
                .Select(static group => new
                {
                    EventType = group.Key,
                    ConsumerTypes = group.Select(static registration => registration.IntegrationEventType)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static type => type, StringComparer.Ordinal)
                        .ToArray()
                })
                .Where(static group => group.ConsumerTypes.Length > 1)
                .OrderBy(static group => group.EventType, StringComparer.Ordinal)
                .ToArray();
            foreach (var duplicateEventTypeGroup in duplicateEventTypeGroups)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateConsumerEventTypes,
                    Location.None,
                    string.Join(", ", duplicateEventTypeGroup.ConsumerTypes),
                    duplicateEventTypeGroup.EventType));
            }

            var ambiguousConsumerTypes = duplicateEventTypeGroups
                .SelectMany(static group => group.ConsumerTypes)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (mappings.Length == 0 && registrations.Length == 0)
            {
                return;
            }

            productionContext.AddSource(
                GeneratedHintName,
                SourceText.From(Emit(mappings, registrations, handlers, ambiguousConsumerTypes), Encoding.UTF8));
        });
    }

    private static void ReportConsumerHandlerDiagnostics(
        SourceProductionContext productionContext,
        string[] consumerTypes,
        ImmutableArray<IntegrationEventHandlerModel> handlers)
    {
        foreach (var consumerType in consumerTypes)
        {
            var handlerCount = handlers.Count(handler =>
                string.Equals(handler.IntegrationEventType, consumerType, StringComparison.Ordinal));
            if (handlerCount == 0)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    MissingConsumerHandler,
                    Location.None,
                    consumerType));
            }
            else if (handlerCount > 1)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    DuplicateConsumerHandlers,
                    Location.None,
                    consumerType,
                    handlerCount));
            }
        }
    }

    private static (IntegrationEventMappingModel? Mapping, string MethodDisplayName, Location Location) BuildMapping(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var method = (IMethodSymbol)context.TargetSymbol;
        var parameterTypes = string.Join(", ", method.Parameters.Select(static parameter => parameter.Type.ToDisplayString(FullyQualifiedFormat)));
        var methodDisplayName = $"{method.ContainingType.ToDisplayString(FullyQualifiedFormat)}.{method.Name}({parameterTypes})";
        var attributeSyntax = context.Attributes[0].ApplicationSyntaxReference?.GetSyntax(cancellationToken);
        var location = attributeSyntax?.GetLocation() ?? method.Locations.FirstOrDefault() ?? Location.None;
        if (method.IsGenericMethod ||
            !method.IsStatic ||
            method.IsAbstract ||
            method.IsVirtual ||
            method.ReturnsByRef ||
            method.ReturnsByRefReadonly ||
            !IsAccessibleToGeneratedCode(method) ||
            method.Parameters.Length != 3 ||
            method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
        {
            return (null, methodDisplayName, location);
        }

        var domainEvent = method.Parameters[0].Type;
        var integrationEvent = method.ReturnType;
        if (!IsValidMappingType(domainEvent, DomainEventInterfaceName) ||
            !IsValidMappingType(integrationEvent, IntegrationEventInterfaceName) ||
            method.Parameters[1].Type.ToDisplayString(FullyQualifiedFormat) != "global::System.Guid" ||
            method.Parameters[2].Type.ToDisplayString(FullyQualifiedFormat) != "global::System.DateTimeOffset")
        {
            return (null, methodDisplayName, location);
        }

        return (
            new IntegrationEventMappingModel(
                method.ContainingType.ToDisplayString(FullyQualifiedFormat),
                method.Name,
                domainEvent.ToDisplayString(FullyQualifiedFormat),
                integrationEvent.ToDisplayString(FullyQualifiedFormat),
                $"Dispatch{Sanitize(domainEvent.ToDisplayString())}",
                EscapeIdentifier(method.Name)),
            methodDisplayName,
            location);
    }

    private static IntegrationEventRegistrationCandidate? BuildRegistration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = (InvocationExpressionSyntax)context.Node;
        var invokedName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            SimpleNameSyntax directName => directName,
            _ => null
        };
        if (invokedName is null ||
            invokedName.Identifier.ValueText is not ("AddIntegrationEventContract" or "AddIntegrationEventConsumer"))
        {
            return null;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method || !IsRegistrationApi(method, context.SemanticModel.Compilation))
        {
            method = symbolInfo.CandidateSymbols
                .OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => IsRegistrationApi(candidate, context.SemanticModel.Compilation));
        }

        if (method is null)
        {
            return null;
        }

        var integrationEventType = ResolveRegistrationType(invokedName, method, context.SemanticModel, cancellationToken);
        if (integrationEventType is null)
        {
            return null;
        }

        var integrationEventTypeName = integrationEventType.ToDisplayString(FullyQualifiedFormat);
        var location = invokedName is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } genericName
            ? genericName.TypeArgumentList.Arguments[0].GetLocation()
            : invocation.GetLocation();
        var hasNullableTypeArgument = invokedName is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } registrationName &&
            registrationName.TypeArgumentList.Arguments[0] is NullableTypeSyntax;
        if (hasNullableTypeArgument || !IsValidIntegrationEventContract(integrationEventType))
        {
            return new IntegrationEventRegistrationCandidate(null, integrationEventTypeName, location);
        }

        return new IntegrationEventRegistrationCandidate(
            new IntegrationEventRegistrationModel(
                integrationEventTypeName,
                method.Name == "AddIntegrationEventConsumer",
                ResolveEventType(invocation, integrationEventType, context.SemanticModel, cancellationToken)),
            integrationEventTypeName,
            location);
    }

    private static ITypeSymbol? ResolveRegistrationType(
        SimpleNameSyntax invokedName,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (invokedName is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } genericName)
        {
            return semanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken).Type;
        }

        return method.TypeArguments.Length == 1 ? method.TypeArguments[0] : null;
    }

    private static string? ResolveEventType(
        InvocationExpressionSyntax invocation,
        ITypeSymbol integrationEventType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var argumentConstant = semanticModel.GetConstantValue(
                invocation.ArgumentList.Arguments[0].Expression,
                cancellationToken);
            if (argumentConstant is { HasValue: true, Value: string argumentEventType })
            {
                return argumentEventType;
            }
        }

        var eventTypeProperty = integrationEventType.GetMembers("EventType")
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property => property.IsStatic && property.Type.SpecialType == SpecialType.System_String);
        foreach (var syntaxReference in eventTypeProperty?.DeclaringSyntaxReferences ?? [])
        {
            if (syntaxReference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            var expression = property.ExpressionBody?.Expression;
            if (expression is null)
            {
                var getter = property.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
                expression = getter?.ExpressionBody?.Expression
                    ?? getter?.Body?.Statements.OfType<ReturnStatementSyntax>().SingleOrDefault()?.Expression;
            }

            if (expression is null)
            {
                continue;
            }

            var expressionModel = semanticModel.Compilation.GetSemanticModel(expression.SyntaxTree);
            var propertyConstant = expressionModel.GetConstantValue(expression, cancellationToken);
            if (propertyConstant is { HasValue: true, Value: string propertyEventType })
            {
                return propertyEventType;
            }
        }

        return null;
    }

    private static ImmutableArray<IntegrationEventHandlerModel> BuildHandlers(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)context.Node, cancellationToken) is not INamedTypeSymbol type ||
            type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            !IsAccessibleToGeneratedCode(type))
        {
            return [];
        }

        var handlerType = type.ToDisplayString(FullyQualifiedFormat);
        return type.AllInterfaces
            .Where(static candidate =>
                string.Equals(candidate.OriginalDefinition.ToDisplayString(), IntegrationEventHandlerInterfaceName, StringComparison.Ordinal) &&
                candidate.TypeArguments.Length == 1 &&
                !ContainsTypeParameters(candidate.TypeArguments[0]))
            .Select(candidate => new IntegrationEventHandlerModel(
                candidate.TypeArguments[0].ToDisplayString(FullyQualifiedFormat),
                handlerType))
            .Distinct()
            .OrderBy(static handler => handler.IntegrationEventType, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool Implements(ITypeSymbol type, string interfaceName) =>
        string.Equals(type.OriginalDefinition.ToDisplayString(), interfaceName, StringComparison.Ordinal) ||
        type.AllInterfaces.Any(interfaceType => string.Equals(interfaceType.OriginalDefinition.ToDisplayString(), interfaceName, StringComparison.Ordinal));

    private static bool IsValidMappingType(ITypeSymbol type, string interfaceName) =>
        type is INamedTypeSymbol
        {
            TypeKind: TypeKind.Class or TypeKind.Struct,
            IsAbstract: false,
            IsRefLikeType: false,
            NullableAnnotation: not NullableAnnotation.Annotated,
        } namedType &&
        Implements(namedType, interfaceName) &&
        IsContractTypeAccessibleToGeneratedCode(namedType);

    private static bool IsValidIntegrationEventContract(ITypeSymbol type)
    {
        return type is INamedTypeSymbol
        {
            TypeKind: TypeKind.Class or TypeKind.Struct,
            IsAbstract: false,
            IsUnboundGenericType: false,
            IsRefLikeType: false,
            IsFileLocal: false,
            CanBeReferencedByName: true
        } namedType &&
            namedType.NullableAnnotation is not NullableAnnotation.Annotated &&
            !ContainsTypeParameters(namedType) &&
            Implements(namedType, IntegrationEventInterfaceName) &&
            IsContractTypeAccessibleToGeneratedCode(namedType);
    }

    private static bool ContainsTypeParameters(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol arrayType => ContainsTypeParameters(arrayType.ElementType),
            IPointerTypeSymbol pointerType => ContainsTypeParameters(pointerType.PointedAtType),
            INamedTypeSymbol namedType => namedType.IsUnboundGenericType || namedType.TypeArguments.Any(ContainsTypeParameters),
            _ => false
        };
    }

    private static bool IsContractTypeAccessibleToGeneratedCode(ITypeSymbol type)
    {
        return type switch
        {
            IArrayTypeSymbol arrayType => IsContractTypeAccessibleToGeneratedCode(arrayType.ElementType),
            IPointerTypeSymbol pointerType => IsContractTypeAccessibleToGeneratedCode(pointerType.PointedAtType),
            INamedTypeSymbol namedType => IsNamedContractTypeAccessibleToGeneratedCode(namedType),
            IDynamicTypeSymbol => true,
            _ => false
        };
    }

    private static bool IsNamedContractTypeAccessibleToGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal ||
                !current.CanBeReferencedByName ||
                !IsAccessibleToGeneratedCode(current.DeclaredAccessibility) ||
                !current.TypeArguments.All(IsContractTypeAccessibleToGeneratedCode))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRegistrationApi(IMethodSymbol method, Compilation compilation)
    {
        var registrationExtensions = compilation.GetTypeByMetadataName(RegistrationExtensionsTypeName);
        var originalMethod = (method.ReducedFrom ?? method).OriginalDefinition;
        return registrationExtensions is not null &&
            registrationExtensions.GetMembers(method.Name)
                .OfType<IMethodSymbol>()
                .Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, originalMethod));
    }

    private static bool IsAccessibleToGeneratedCode(IMethodSymbol method) =>
        IsAccessibleToGeneratedCode(method.DeclaredAccessibility) &&
        IsAccessibleToGeneratedCode(method.ContainingType);

    private static bool IsAccessibleToGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal ||
                current.TypeParameters.Length != 0 ||
                !IsAccessibleToGeneratedCode(current.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAccessibleToGeneratedCode(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static string Emit(
        ImmutableArray<IntegrationEventMappingModel> mappings,
        ImmutableArray<IntegrationEventRegistrationModel> registrations,
        ImmutableArray<IntegrationEventHandlerModel> handlers,
        string[] ambiguousConsumerTypes)
    {
        var contractTypes = mappings.Select(static mapping => mapping.IntegrationEventType)
            .Concat(registrations.Select(static registration => registration.IntegrationEventType))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static type => type, StringComparer.Ordinal)
            .ToArray();
        var consumerTypes = registrations.Where(static registration => registration.IsConsumer)
            .Select(static registration => registration.IntegrationEventType)
            .Distinct(StringComparer.Ordinal)
            .Where(type => !ambiguousConsumerTypes.Contains(type, StringComparer.Ordinal))
            .Where(type => handlers.Count(handler => string.Equals(handler.IntegrationEventType, type, StringComparison.Ordinal)) == 1)
            .OrderBy(static type => type, StringComparer.Ordinal)
            .ToArray();
        var consumerHandlers = handlers
            .Where(handler => consumerTypes.Contains(handler.IntegrationEventType, StringComparer.Ordinal))
            .OrderBy(static handler => handler.IntegrationEventType, StringComparer.Ordinal)
            .ThenBy(static handler => handler.HandlerType, StringComparer.Ordinal)
            .ToArray();
        var consumerHandlerTypes = consumerHandlers
            .Select(static handler => handler.HandlerType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static handler => handler, StringComparer.Ordinal)
            .ToArray();
        var builder = new StringBuilder("// <auto-generated />\n#nullable enable\n\nnamespace SharedKernel.Messaging.IntegrationEvents.Generated\n{\n\n");

        if (contractTypes.Length > 0)
        {
            EmitSerializer(builder, contractTypes);
        }

        if (consumerTypes.Length > 0)
        {
            EmitHandlerForwarder(builder);
            EmitEnvelopePublisher(builder, contractTypes, consumerTypes);
        }

        if (mappings.Length > 0)
        {
            EmitDomainEventDispatcher(builder, mappings);
        }

        builder.AppendLine("}");
        EmitDependencyInjection(
            builder,
            mappings.Length > 0,
            contractTypes,
            consumerTypes,
            consumerHandlers,
            consumerHandlerTypes);
        return builder.ToString();
    }

    private static void EmitSerializer(StringBuilder builder, string[] contractTypes)
    {
        builder.AppendLine("internal sealed class GeneratedIntegrationEventSerializer(");
        for (var index = 0; index < contractTypes.Length; index++)
        {
            builder.Append("    global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<").Append(contractTypes[index]).Append("> jsonTypeInfo").Append(index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            builder.AppendLine(index == contractTypes.Length - 1 ? ") : global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventSerializer" : ",");
        }
        builder.AppendLine("{");
        builder.AppendLine("    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent) where TIntegrationEvent : global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEvent");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(integrationEvent);");
        builder.AppendLine("        return integrationEvent switch");
        builder.AppendLine("        {");
        for (var index = 0; index < contractTypes.Length; index++)
        {
            builder.Append("            ").Append(contractTypes[index]).Append(" typed when integrationEvent.GetType() == typeof(").Append(contractTypes[index]).Append(") => global::System.Text.Json.JsonSerializer.Serialize(typed, jsonTypeInfo").Append(index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine("),");
        }
        builder.AppendLine("            _ => throw new global::System.NotSupportedException($\"Integration event type '{integrationEvent.GetType().FullName}' is not registered for durable serialization.\"),");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitHandlerForwarder(StringBuilder builder)
    {
        builder.AppendLine("internal sealed class GeneratedIntegrationEventHandlerForwarder<TIntegrationEvent, THandler>(THandler handler) : global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventHandler<TIntegrationEvent>");
        builder.AppendLine("    where TIntegrationEvent : global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEvent");
        builder.AppendLine("    where THandler : class, global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventHandler<TIntegrationEvent>");
        builder.AppendLine("{");
        builder.AppendLine("    public global::System.Threading.Tasks.ValueTask Handle(TIntegrationEvent integrationEvent, global::System.Threading.CancellationToken ct) =>");
        builder.AppendLine("        handler.Handle(integrationEvent, ct);");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitEnvelopePublisher(StringBuilder builder, string[] contractTypes, string[] consumerTypes)
    {
        builder.AppendLine("internal sealed class GeneratedIntegrationEventEnvelopePublisher(");
        for (var index = 0; index < consumerTypes.Length; index++)
        {
            builder.Append("    global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventHandler<").Append(consumerTypes[index]).Append("> handler").Append(index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",");
        }
        for (var index = 0; index < contractTypes.Length; index++)
        {
            builder.Append("    global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<").Append(contractTypes[index]).Append("> jsonTypeInfo").Append(index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
            builder.AppendLine(index == contractTypes.Length - 1 ? ") : global::SharedKernel.Messaging.IEventEnvelopePublisher" : ",");
        }
        builder.AppendLine("{");
        builder.AppendLine("    public async global::System.Threading.Tasks.ValueTask Publish(global::SharedKernel.Messaging.EventEnvelope envelope, global::System.Threading.CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(envelope);");
        builder.AppendLine("        ct.ThrowIfCancellationRequested();");
        for (var index = 0; index < consumerTypes.Length; index++)
        {
            var contractIndex = Array.IndexOf(contractTypes.ToArray(), consumerTypes[index]);
            var suffix = index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            builder.Append("        if (global::System.String.Equals(envelope.EventType, ").Append(consumerTypes[index]).AppendLine(".EventType, global::System.StringComparison.Ordinal))");
            builder.AppendLine("        {");
            builder.AppendLine("            if (envelope.Payload is null) throw new global::System.InvalidOperationException($\"Integration event '{envelope.EventType}' payload is required.\");");
            builder.Append("            var integrationEvent = global::System.Text.Json.JsonSerializer.Deserialize(envelope.Payload, jsonTypeInfo").Append(contractIndex.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(")");
            builder.AppendLine("                ?? throw new global::System.InvalidOperationException($\"Integration event '{envelope.EventType}' payload could not be deserialized.\");");
            builder.Append("            await handler").Append(suffix).AppendLine(".Handle(integrationEvent, ct).ConfigureAwait(false);");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
        }
        builder.AppendLine("        throw new global::System.NotSupportedException($\"Integration event envelope type '{envelope.EventType}' is not registered for delivery.\");");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitDomainEventDispatcher(StringBuilder builder, ImmutableArray<IntegrationEventMappingModel> mappings)
    {
        builder.AppendLine("internal sealed class GeneratedIntegrationEventDomainEventDispatcher(global::SharedKernel.Messaging.IntegrationEvents.IDomainEventIntegrationEventOutbox outbox, global::System.TimeProvider timeProvider) : global::SharedKernel.Domain.IDomainEventDispatchHandler");
        builder.AppendLine("{");
        builder.AppendLine("    public global::System.Threading.Tasks.ValueTask Handle(global::SharedKernel.Domain.IDomainEvent domainEvent, global::System.Threading.CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(domainEvent);");
        builder.AppendLine("        ct.ThrowIfCancellationRequested();");
        builder.AppendLine("        return domainEvent switch");
        builder.AppendLine("        {");
        foreach (var domainEventType in mappings.Select(static mapping => mapping.DomainEventType).Distinct(StringComparer.Ordinal))
        {
            var method = mappings.First(mapping => string.Equals(mapping.DomainEventType, domainEventType, StringComparison.Ordinal)).DispatchMethodName;
            builder.Append("            ").Append(domainEventType).Append(" typedDomainEvent when domainEvent.GetType() == typeof(").Append(domainEventType).Append(") => ").Append(method).AppendLine("(typedDomainEvent, ct),");
        }
        builder.AppendLine("            _ => global::System.Threading.Tasks.ValueTask.CompletedTask,");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        foreach (var group in mappings.GroupBy(static mapping => mapping.DomainEventType).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var first = group.First();
            builder.Append("    private async global::System.Threading.Tasks.ValueTask ").Append(first.DispatchMethodName).Append('(').Append(group.Key).AppendLine(" domainEvent, global::System.Threading.CancellationToken ct)");
            builder.AppendLine("    {");
            builder.AppendLine("        ct.ThrowIfCancellationRequested();");
            foreach (var mapping in group
                .OrderBy(static mapping => mapping.IntegrationEventType, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.ContainingType, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.MethodName, StringComparer.Ordinal))
            {
                builder.AppendLine("        await outbox.Enqueue(");
                builder.Append("            ").Append(mapping.ContainingType).Append('.').Append(mapping.EscapedMethodName).AppendLine("(");
                builder.AppendLine("                domainEvent,");
                builder.AppendLine("                global::System.Guid.CreateVersion7(),");
                builder.AppendLine("                timeProvider.GetUtcNow()),");
                builder.AppendLine("            ct).ConfigureAwait(false);");
            }
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitDependencyInjection(
        StringBuilder builder,
        bool hasMappings,
        string[] contractTypes,
        string[] consumerTypes,
        IntegrationEventHandlerModel[] consumerHandlers,
        string[] consumerHandlerTypes)
    {
        builder.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine("internal static class GeneratedIntegrationEventServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedIntegrationEvents(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(services);");
        for (var firstIndex = 0; firstIndex < contractTypes.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < contractTypes.Length; secondIndex++)
            {
                builder.Append("        if (global::System.String.Equals(").Append(contractTypes[firstIndex]).Append(".EventType, ").Append(contractTypes[secondIndex]).AppendLine(".EventType, global::System.StringComparison.Ordinal))");
                builder.AppendLine("        {");
                builder.Append("            throw new global::System.InvalidOperationException($\"Integration event contracts '{typeof(").Append(contractTypes[firstIndex]).Append(").FullName}' and '{typeof(").Append(contractTypes[secondIndex]).Append(").FullName}' declare duplicate event type '{(").Append(contractTypes[firstIndex]).AppendLine(".EventType)}'.\");");
                builder.AppendLine("        }");
            }
        }
        if (hasMappings)
        {
            builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, global::System.TimeProvider.System);");
            builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<global::SharedKernel.Domain.IDomainEventDispatcher, global::SharedKernel.Domain.CompositeDomainEventDispatcher>(services);");
            builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::SharedKernel.Domain.IDomainEventDispatchHandler, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventDomainEventDispatcher>());");
        }
        if (contractTypes.Length > 0)
        {
            builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton<global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventSerializer, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventSerializer>(services);");
        }
        foreach (var consumerHandlerType in consumerHandlerTypes)
        {
            builder.Append("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<").Append(consumerHandlerType).AppendLine(">(services);");
        }
        foreach (var consumerHandler in consumerHandlers)
        {
            builder.Append("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<global::SharedKernel.Messaging.IntegrationEvents.IIntegrationEventHandler<")
                .Append(consumerHandler.IntegrationEventType)
                .Append(">, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventHandlerForwarder<")
                .Append(consumerHandler.IntegrationEventType)
                .Append(", ")
                .Append(consumerHandler.HandlerType)
                .AppendLine(">>(services);");
        }
        if (consumerTypes.Length > 0)
        {
            builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<global::SharedKernel.Messaging.IEventEnvelopePublisher, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventEnvelopePublisher>(services);");
        }
        builder.AppendLine("        return services;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("}");
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Where(char.IsLetterOrDigit))
        {
            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string EscapeIdentifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) is not SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(value) is not SyntaxKind.None
            ? $"@{value}"
            : value;
}
