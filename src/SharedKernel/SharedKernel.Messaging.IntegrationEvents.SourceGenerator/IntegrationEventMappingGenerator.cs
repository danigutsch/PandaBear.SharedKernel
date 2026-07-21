using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

/// <summary>
/// Generates domain event dispatch handlers for attributed integration event mapping methods.
/// </summary>
[Generator]
public sealed class IntegrationEventMappingGenerator : IIncrementalGenerator
{
    private const string AttributeName = "SharedKernel.Messaging.IntegrationEvents.IntegrationEventMappingAttribute";
    private const string DomainEventInterfaceName = "SharedKernel.Domain.IDomainEvent";
    private const string IntegrationEventInterfaceName = "SharedKernel.Messaging.IntegrationEvents.IIntegrationEvent";

    private static readonly DiagnosticDescriptor InvalidMappingDiagnostic = new(
        "INTEGRATIONEVENT001",
        "Invalid integration event mapping",
        "Integration event mapping method '{0}' must be an accessible, concrete, non-generic static method in a non-generic type with exactly three by-value parameters: a non-null IDomainEvent, Guid, and DateTimeOffset, and return a non-null concrete IIntegrationEvent",
        "IntegrationEvents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mappings = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax,
                static (attributeContext, cancellationToken) => BuildMapping(attributeContext, cancellationToken))
            .Collect()
            .Select(static (mappings, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return mappings
                    .OrderBy(static mapping => mapping.DomainEventType, StringComparer.Ordinal)
                    .ThenBy(static mapping => mapping.IntegrationEventType, StringComparer.Ordinal)
                    .ThenBy(static mapping => mapping.MethodName, StringComparer.Ordinal)
                    .ToImmutableArray();
            })
            .WithTrackingName("IntegrationEventMappings");

        context.RegisterSourceOutput(mappings, static (productionContext, mappings) =>
        {
            productionContext.CancellationToken.ThrowIfCancellationRequested();
            foreach (var mapping in mappings.Where(static mapping => !mapping.IsValid))
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    InvalidMappingDiagnostic,
                    mapping.Location,
                    mapping.MethodName));
            }

            var validMappings = mappings.Where(static mapping => mapping.IsValid).ToImmutableArray();
            if (validMappings.Length == 0)
            {
                return;
            }

            productionContext.AddSource(
                "SharedKernel.Messaging.IntegrationEvents.GeneratedIntegrationEventMappings.g.cs",
                SourceText.From(Emit(validMappings, productionContext.CancellationToken), Encoding.UTF8));
        });
    }

    private static IntegrationEventMappingModel BuildMapping(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var method = (IMethodSymbol)context.TargetSymbol;
        if (method.IsGenericMethod ||
            method.IsAbstract ||
            method.IsVirtual ||
            !method.IsStatic ||
            IsInGenericContainingType(method.ContainingType) ||
            !IsAccessibleToGeneratedCode(method) ||
            method.Parameters.Length != 3)
        {
            return IntegrationEventMappingModel.Invalid(method.Name, method.Locations.FirstOrDefault());
        }

        var domainEvent = method.Parameters[0].Type;
        var integrationEvent = method.ReturnType;
        if (!Implements(domainEvent, DomainEventInterfaceName) ||
            !Implements(integrationEvent, IntegrationEventInterfaceName) ||
            domainEvent.TypeKind == TypeKind.Interface ||
            domainEvent is INamedTypeSymbol { IsAbstract: true } ||
            integrationEvent.TypeKind == TypeKind.Interface ||
            integrationEvent is INamedTypeSymbol { IsAbstract: true } ||
            domainEvent.NullableAnnotation == NullableAnnotation.Annotated ||
            integrationEvent.NullableAnnotation == NullableAnnotation.Annotated ||
            method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
        {
            return IntegrationEventMappingModel.Invalid(method.Name, method.Locations.FirstOrDefault());
        }

        if (method.Parameters[1].Type.ToDisplayString(FullyQualifiedFormat) != "global::System.Guid" ||
            method.Parameters[2].Type.ToDisplayString(FullyQualifiedFormat) != "global::System.DateTimeOffset")
        {
            return IntegrationEventMappingModel.Invalid(method.Name, method.Locations.FirstOrDefault());
        }

        var containingType = method.ContainingType.ToDisplayString(FullyQualifiedFormat);
        var domainEventType = domainEvent.ToDisplayString(FullyQualifiedFormat);
        var integrationEventType = integrationEvent.ToDisplayString(FullyQualifiedFormat);
        var dispatchMethodName = $"Dispatch{Sanitize(domainEvent.ToDisplayString())}";

        return new IntegrationEventMappingModel(
            true,
            containingType,
            method.Name,
            method.Locations.FirstOrDefault(),
            domainEventType,
            integrationEventType,
            dispatchMethodName,
            EscapeIdentifier(method.Name));
    }

    private static bool Implements(ITypeSymbol type, string interfaceName)
    {
        return string.Equals(type.OriginalDefinition.ToDisplayString(), interfaceName, StringComparison.Ordinal) ||
            type.AllInterfaces.Any(interfaceType =>
            string.Equals(interfaceType.OriginalDefinition.ToDisplayString(), interfaceName, StringComparison.Ordinal));
    }

    private static bool IsAccessibleToGeneratedCode(ISymbol method)
    {
        for (ISymbol? symbol = method; symbol is not null; symbol = symbol.ContainingType)
        {
            if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInGenericContainingType(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : $"@{identifier}";

    private static string Emit(
        ImmutableArray<IntegrationEventMappingModel> mappings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable

            namespace SharedKernel.Messaging.IntegrationEvents.Generated
            {

            """);

        builder.AppendLine($$"""
            internal sealed class GeneratedIntegrationEventDomainEventDispatcher(
                global::SharedKernel.Messaging.IntegrationEvents.IDomainEventIntegrationEventOutbox outbox,
                global::System.TimeProvider timeProvider)
                : global::SharedKernel.DomainEvents.IDomainEventDispatchHandler
            {
                public global::System.Threading.Tasks.ValueTask Handle(global::SharedKernel.Domain.IDomainEvent domainEvent, global::System.Threading.CancellationToken ct)
                {
                    global::System.ArgumentNullException.ThrowIfNull(domainEvent);

                    return domainEvent switch
                    {

            """);

        foreach (var domainEventType in mappings.Select(static mapping => mapping.DomainEventType).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatchMethodName = mappings.First(mapping => string.Equals(mapping.DomainEventType, domainEventType, StringComparison.Ordinal)).DispatchMethodName;
            builder.AppendLine($"            {domainEventType} typedDomainEvent when domainEvent.GetType() == typeof({domainEventType}) => {dispatchMethodName}(typedDomainEvent, ct),");
        }

        builder.AppendLine("            _ => global::System.Threading.Tasks.ValueTask.CompletedTask,");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (var group in mappings.GroupBy(static mapping => mapping.DomainEventType).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var firstMapping = group.First();
            builder.AppendLine($"    private async global::System.Threading.Tasks.ValueTask {firstMapping.DispatchMethodName}({group.Key} domainEvent, global::System.Threading.CancellationToken ct)");
            builder.AppendLine("    {");

            foreach (var mapping in group.OrderBy(static mapping => mapping.IntegrationEventType, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine("        await outbox.Enqueue(");
                builder.AppendLine($"            {mapping.ContainingType}.{mapping.EscapedMethodName}(");
                builder.AppendLine("                domainEvent,");
                builder.AppendLine("                global::System.Guid.CreateVersion7(),");
                builder.AppendLine("                timeProvider.GetUtcNow()),");
                builder.AppendLine("            ct).ConfigureAwait(false);");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedIntegrationEventMappingsServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedIntegrationEventMappings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(services);");
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, global::System.TimeProvider.System);");
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<global::SharedKernel.DomainEvents.IDomainEventDispatcher, global::SharedKernel.DomainEvents.CompositeDomainEventDispatcher>(services);");
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::SharedKernel.DomainEvents.IDomainEventDispatchHandler, global::SharedKernel.Messaging.IntegrationEvents.Generated.GeneratedIntegrationEventDomainEventDispatcher>());");

        builder.AppendLine("        return services;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("}");

        return builder.ToString();
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
}
