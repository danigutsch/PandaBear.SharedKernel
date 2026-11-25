using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.AuditTrail.SourceGenerator;

/// <summary>Generates domain-event dispatch handlers for attributed audit-trail mapping methods.</summary>
[Generator]
public sealed class AuditTrailMappingGenerator : IIncrementalGenerator
{
    private const string AttributeName = "SharedKernel.AuditTrail.AuditTrailMappingAttribute";
    private const string AuditTrailEntryInterfaceName = "SharedKernel.AuditTrail.IAuditTrailEntry";
    private const string DomainEventInterfaceName = "SharedKernel.Domain.IDomainEvent";

    private static readonly DiagnosticDescriptor InvalidMappingDiagnostic = new(
        "AUDIT001",
        "Invalid audit trail mapping",
        "Audit trail mapping method '{0}' must be an accessible non-generic static method with exactly two parameters: an IDomainEvent and DateTimeOffset, and return an IAuditTrailEntry",
        "AuditTrail",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMappingDiagnostic = new(
        "AUDIT002",
        "Duplicate audit trail mapping",
        "Domain event type '{0}' has more than one audit trail mapping",
        "AuditTrail",
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
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax,
                static (attributeContext, cancellationToken) => BuildCandidate(attributeContext, cancellationToken))
            .Collect()
            .Select(static (candidates, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return candidates
                    .OrderBy(static candidate => candidate.SortKey, StringComparer.Ordinal)
                    .ToImmutableArray();
            })
            .WithTrackingName("AuditTrailMappings");

        context.RegisterSourceOutput(candidates, static (productionContext, candidates) => Emit(productionContext, candidates));
    }

    private static AuditTrailMappingCandidate BuildCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var method = (IMethodSymbol)context.TargetSymbol;
        var location = method.Locations.FirstOrDefault();
        var sortKey = $"{method.ContainingType.ToDisplayString(FullyQualifiedFormat)}.{method.Name}({string.Join(",", method.Parameters.Select(static parameter => parameter.Type.ToDisplayString(FullyQualifiedFormat)))})";
        if (method.IsGenericMethod ||
            method.IsAbstract ||
            method.IsVirtual ||
            IsInGenericContainingType(method.ContainingType) ||
            !method.IsStatic ||
            !IsAccessibleToGeneratedCode(method) ||
            method.Parameters.Length != 2)
        {
            return AuditTrailMappingCandidate.Invalid(method.Name, sortKey, location);
        }

        var domainEvent = method.Parameters[0].Type;
        var auditTrailEntry = method.ReturnType;
        if (!Implements(domainEvent, DomainEventInterfaceName) ||
            domainEvent.TypeKind == TypeKind.Interface ||
            domainEvent is INamedTypeSymbol { IsAbstract: true } ||
            !Implements(auditTrailEntry, AuditTrailEntryInterfaceName) ||
            domainEvent.NullableAnnotation == NullableAnnotation.Annotated ||
            auditTrailEntry.NullableAnnotation == NullableAnnotation.Annotated ||
            method.Parameters[0].RefKind != RefKind.None ||
            method.Parameters[1].RefKind != RefKind.None ||
            method.Parameters[1].Type.ToDisplayString(FullyQualifiedFormat) != "global::System.DateTimeOffset")
        {
            return AuditTrailMappingCandidate.Invalid(method.Name, sortKey, location);
        }

        return AuditTrailMappingCandidate.Valid(
            EscapeIdentifier(method.Name),
            sortKey,
            location,
            method.ContainingType.ToDisplayString(FullyQualifiedFormat),
            domainEvent.ToDisplayString(FullyQualifiedFormat),
            auditTrailEntry.ToDisplayString(FullyQualifiedFormat));
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<AuditTrailMappingCandidate> candidates)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        foreach (var candidate in candidates.Where(static candidate => !candidate.IsValid))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.ReportDiagnostic(Diagnostic.Create(InvalidMappingDiagnostic, candidate.Location, candidate.MethodName));
        }

        var validCandidates = candidates.Where(static candidate => candidate.IsValid).ToImmutableArray();
        var duplicateEventTypes = validCandidates
            .GroupBy(static candidate => candidate.DomainEventType, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);
        foreach (var candidate in validCandidates.Where(candidate => duplicateEventTypes.Contains(candidate.DomainEventType)))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.ReportDiagnostic(Diagnostic.Create(DuplicateMappingDiagnostic, candidate.Location, candidate.DomainEventType));
        }

        var mappings = validCandidates
            .Where(candidate => !duplicateEventTypes.Contains(candidate.DomainEventType))
            .ToImmutableArray();
        if (mappings.Length == 0)
        {
            return;
        }

        context.AddSource(
            "SharedKernel.AuditTrail.GeneratedAuditTrailMappings.g.cs",
            SourceText.From(EmitMappings(mappings, context.CancellationToken), Encoding.UTF8));
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

    private static string EmitMappings(ImmutableArray<AuditTrailMappingCandidate> mappings, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable

            namespace SharedKernel.AuditTrail.Generated
            {

            """);

        for (var index = 0; index < mappings.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = mappings[index];
            builder.AppendLine($$"""
                internal sealed class GeneratedAuditTrailDomainEventHandler{{index}}(
                    global::SharedKernel.AuditTrail.IAuditTrailSink<{{mapping.AuditTrailEntryType}}> auditTrailSink,
                    global::System.TimeProvider timeProvider)
                    : global::SharedKernel.Domain.IDomainEventDispatchHandler
                {
                    public global::System.Threading.Tasks.ValueTask Handle(global::SharedKernel.Domain.IDomainEvent domainEvent, global::System.Threading.CancellationToken ct)
                    {
                        global::System.ArgumentNullException.ThrowIfNull(domainEvent);

                        return domainEvent.GetType() == typeof({{mapping.DomainEventType}})
                            ? auditTrailSink.Append(
                                {{mapping.ContainingType}}.{{mapping.MethodName}}(({{mapping.DomainEventType}})domainEvent, timeProvider.GetUtcNow()),
                                ct)
                            : global::System.Threading.Tasks.ValueTask.CompletedTask;
                    }
                }

                """);
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
        builder.AppendLine("{");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedAuditTrailMappingsServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedAuditTrailMappings(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(services);");
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, global::System.TimeProvider.System);");
        builder.AppendLine("        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<global::SharedKernel.Domain.IDomainEventDispatcher, global::SharedKernel.Domain.CompositeDomainEventDispatcher>(services);");

        for (var index = 0; index < mappings.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine($"        global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::SharedKernel.Domain.IDomainEventDispatchHandler, global::SharedKernel.AuditTrail.Generated.GeneratedAuditTrailDomainEventHandler{index}>());");
        }

        builder.AppendLine("        return services;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private sealed class AuditTrailMappingCandidate
    {
        private AuditTrailMappingCandidate(
            string methodName,
            string sortKey,
            Location? location,
            string? containingType,
            string? domainEventType,
            string? auditTrailEntryType)
        {
            MethodName = methodName;
            SortKey = sortKey;
            Location = location;
            ContainingType = containingType;
            DomainEventType = domainEventType;
            AuditTrailEntryType = auditTrailEntryType;
        }

        public string MethodName { get; }

        public string SortKey { get; }

        public Location? Location { get; }

        public string? ContainingType { get; }

        public string? DomainEventType { get; }

        public string? AuditTrailEntryType { get; }

        public bool IsValid => ContainingType is not null && DomainEventType is not null && AuditTrailEntryType is not null;

        public static AuditTrailMappingCandidate Invalid(string methodName, string sortKey, Location? location) =>
            new(methodName, sortKey, location, null, null, null);

        public static AuditTrailMappingCandidate Valid(
            string methodName,
            string sortKey,
            Location? location,
            string containingType,
            string domainEventType,
            string auditTrailEntryType) =>
            new(methodName, sortKey, location, containingType, domainEventType, auditTrailEntryType);
    }
}
