using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Domain.Analyzers;

/// <summary>
/// Reports diagnostics for SharedKernel domain-model encapsulation conventions.
/// </summary>
/// <remarks>
/// Reference this analyzer only from domain-model projects. Every property in the compilation is
/// treated as domain state, so the project reference is the activation boundary.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelDomainAnalyzer : DiagnosticAnalyzer
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

    private static readonly DiagnosticDescriptor NonPrivateSetterRule = new(
        DomainDiagnosticIds.NonPrivateSetter,
        title: "Domain-model properties should not expose non-private setters",
        messageFormat: "Property '{0}' exposes a non-private setter; change domain state through an intent-revealing method and make the setter private",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Domain state should change only through behavior that protects invariants. Use a private setter, an init accessor, or a get-only property, and expose an intent-revealing method for each change.");

    private static readonly DiagnosticDescriptor MutableCollectionExposureRule = new(
        DomainDiagnosticIds.MutableCollectionExposure,
        title: "Domain-model properties should not expose mutable collections",
        messageFormat: "Property '{0}' exposes mutable collection type '{1}'; expose IReadOnlyCollection<T> or IReadOnlyList<T> over a private collection and add intent-revealing methods",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Callers outside the assembly must not add, remove, or replace domain-owned items directly. Expose a read-only view and keep changes behind aggregate methods. Arrays and List<T> are left to CA1819 and CA1002.");

    private static readonly DiagnosticDescriptor AmbientClockRule = new(
        DomainDiagnosticIds.AmbientClock,
        title: "Domain code should not read the ambient clock",
        messageFormat: "Use an injected TimeProvider instead of '{0}' so time is controllable in tests and the business time zone is explicit",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DateTime.Now, UtcNow, Today and DateTimeOffset.Now, UtcNow read the machine clock and local time zone, which makes domain behavior hard to test and hides the business time zone. Pass the date or time in as an argument or use an injected TimeProvider.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } =
        ImmutableArray.Create(NonPrivateSetterRule, MutableCollectionExposureRule, AmbientClockRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var collectionTypes = new MutableCollectionTypes(compilationContext.Compilation);
            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeProperty(symbolContext, collectionTypes),
                SymbolKind.Property);

            var dateTime = compilationContext.Compilation.GetTypeByMetadataName("System.DateTime");
            var dateTimeOffset = compilationContext.Compilation.GetTypeByMetadataName("System.DateTimeOffset");
            if (dateTime is null && dateTimeOffset is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzePropertyReference(operationContext, dateTime, dateTimeOffset),
                OperationKind.PropertyReference);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, MutableCollectionTypes collectionTypes)
    {
        if (context.Symbol is not IPropertySymbol property || property.IsImplicitlyDeclared || property.IsOverride)
        {
            return;
        }

        ReportNonPrivateSetter(context, property);
        ReportMutableCollectionExposure(context, property, collectionTypes);
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        INamedTypeSymbol? dateTime,
        INamedTypeSymbol? dateTimeOffset)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        var property = reference.Property;
        if (!property.IsStatic || property.Name is not ("Now" or "UtcNow" or "Today"))
        {
            return;
        }

        var containingType = property.ContainingType;
        var isDateTime = SymbolEqualityComparer.Default.Equals(containingType, dateTime);
        var isDateTimeOffset = SymbolEqualityComparer.Default.Equals(containingType, dateTimeOffset)
            && property.Name is ("Now" or "UtcNow");
        if (!isDateTime && !isDateTimeOffset)
        {
            return;
        }

        for (var parent = reference.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is INameOfOperation)
            {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(AmbientClockRule, reference.Syntax.GetLocation(), containingType.Name + "." + property.Name));
    }

    private static void ReportNonPrivateSetter(SymbolAnalysisContext context, IPropertySymbol property)
    {
        var setter = property.SetMethod;
        if (setter is null || setter.IsInitOnly)
        {
            return;
        }

        if (setter.DeclaredAccessibility == Accessibility.Private && property.ExplicitInterfaceImplementations.IsEmpty)
        {
            return;
        }

        var location = setter.Locations.FirstOrDefault() ?? property.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(NonPrivateSetterRule, location, property.Name));
    }

    private static void ReportMutableCollectionExposure(
        SymbolAnalysisContext context,
        IPropertySymbol property,
        MutableCollectionTypes collectionTypes)
    {
        if (!IsVisibleOutsideAssembly(property) || !collectionTypes.IsMutableCollection(property.Type))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MutableCollectionExposureRule,
                property.Locations[0],
                property.Name,
                property.Type.ToDisplayString(TypeDisplayFormat)));
    }

    private static bool IsVisibleOutsideAssembly(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }
}
