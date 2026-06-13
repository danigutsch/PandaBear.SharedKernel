using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers;

/// <summary>
/// Reports diagnostics for repository-specific style rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelStyleAnalyzer : DiagnosticAnalyzer
{
    private const string AsyncSuffix = "Async";
    private const string CancellationTokenParameterName = "ct";
    private static readonly DiagnosticDescriptor AsyncSuffixRule = new(
        StyleDiagnosticIds.AsyncSuffix,
        title: "Method name should not end with Async",
        messageFormat: "Method '{0}' should be named '{1}' to follow repository async naming conventions",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules prohibit the Async suffix on method names unless an inherited contract requires that name.");
    private static readonly DiagnosticDescriptor CancellationTokenParameterNameRule = new(
        StyleDiagnosticIds.CancellationTokenParameterName,
        title: "CancellationToken parameters should be named ct",
        messageFormat: "CancellationToken parameter '{0}' should be named 'ct'",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules standardize CancellationToken parameter names on the short form 'ct'.");
    private static readonly DiagnosticDescriptor CancellationTokenDefaultValueRule = new(
        StyleDiagnosticIds.CancellationTokenDefaultValue,
        title: "CancellationToken parameters should not declare default values",
        messageFormat: "CancellationToken parameter '{0}' should not declare a default value",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules require callers to pass cancellation tokens explicitly instead of relying on optional default token parameters.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AsyncSuffixRule, CancellationTokenParameterNameRule, CancellationTokenDefaultValueRule);

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
        var cancellationTokenType = context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");

        context.RegisterSymbolAction(
            symbolContext =>
            {
                var sourceLocation = symbolContext.Symbol.Locations.FirstOrDefault(static location => location.IsInSource);
                AnalyzeMethod(
                    symbolContext,
                    StyleAnalyzerConfigOptions.Parse(
                        symbolContext.Options.AnalyzerConfigOptionsProvider,
                        sourceLocation?.SourceTree));
            },
            SymbolKind.Method);

        if (cancellationTokenType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeParameter(syntaxContext, cancellationTokenType),
            SyntaxKind.Parameter);
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

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, INamedTypeSymbol cancellationTokenType)
    {
        if (context.Node is not ParameterSyntax parameterSyntax
            || context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken) is not IParameterSymbol parameter
            || !parameter.Locations.Any(static location => location.IsInSource)
            || !SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
        {
            return;
        }

        if (!string.Equals(parameter.Name, CancellationTokenParameterName, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    CancellationTokenParameterNameRule,
                    parameter.Locations.First(),
                    parameter.Name));
        }

        if (parameter.HasExplicitDefaultValue)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    CancellationTokenDefaultValueRule,
                    parameter.Locations.First(),
                    parameter.Name));
        }
    }
}
