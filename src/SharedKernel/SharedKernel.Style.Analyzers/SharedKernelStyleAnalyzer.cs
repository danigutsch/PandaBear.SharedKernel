using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers;

/// <summary>
/// Reports diagnostics for repository-specific style rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelStyleAnalyzer : DiagnosticAnalyzer
{
    private const string AsyncSuffix = "Async";
    private static readonly DiagnosticDescriptor AsyncSuffixRule = new(
        StyleDiagnosticIds.AsyncSuffix,
        title: "Method name should not end with Async",
        messageFormat: "Method '{0}' should be named '{1}' to follow repository async naming conventions",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules prohibit the Async suffix on method names unless an inherited contract requires that name.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AsyncSuffixRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeCompilation);
    }

    private static void InitializeCompilation(CompilationStartAnalysisContext context)
    {
        var options = StyleAnalyzerConfigOptions.Parse(context.Options.AnalyzerConfigOptionsProvider);
        context.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, options), SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, StyleAnalyzerConfigOptions options)
    {
        if (context.Symbol is not IMethodSymbol method
            || method.MethodKind != MethodKind.Ordinary
            || method.AssociatedSymbol is not null
            || method.Name.Length <= AsyncSuffix.Length
            || !method.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
        {
            return;
        }

        if (options.AllowAsyncSuffixOverrides && (method.IsOverride || OverridesAsyncContract(method)))
        {
            return;
        }

        if (options.AllowAsyncSuffixInterfaceImplementations && ImplementsInterfaceContract(method))
        {
            return;
        }

        var suggestedName = method.Name.Substring(0, method.Name.Length - AsyncSuffix.Length);
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                AsyncSuffixRule,
                method.Locations.FirstOrDefault(),
                method.Name,
                suggestedName));
    }

    private static bool ImplementsInterfaceContract(IMethodSymbol method)
    {
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            return true;
        }

        return method.ContainingType.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
            .Any(interfaceMethod => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod),
                method));
    }

    private static bool OverridesAsyncContract(IMethodSymbol method)
    {
        var overriddenMethod = method.OverriddenMethod;
        while (overriddenMethod is not null)
        {
            if (overriddenMethod.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
            {
                return true;
            }

            overriddenMethod = overriddenMethod.OverriddenMethod;
        }

        return false;
    }
}
