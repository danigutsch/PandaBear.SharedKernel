using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SharedKernel.Testing.Analyzers;

public sealed partial class TestingAnalyzer
{
    private static readonly DiagnosticDescriptor PlaywrightFixedDelayRule = new(
        TestingDiagnosticIds.PlaywrightFixedDelay,
        title: "Wait for an observable browser condition",
        messageFormat: "Use an observable browser condition or event instead of {0}.WaitForTimeoutAsync",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Playwright fixed delays can finish before the browser is ready. Wait for a locator, an assertion, or an event that represents readiness.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest024");

    private static void RegisterPlaywrightFixedDelayAnalysis(CompilationStartAnalysisContext context)
    {
        var playwright = context.Compilation.SourceModule.ReferencedAssemblySymbols
            .FirstOrDefault(static assembly => assembly.Name == "Microsoft.Playwright");
        if (playwright is null)
        {
            return;
        }

        var page = playwright.GetTypeByMetadataName("Microsoft.Playwright.IPage");
        var frame = playwright.GetTypeByMetadataName("Microsoft.Playwright.IFrame");
        context.RegisterOperationAction(
            operationContext => AnalyzePlaywrightFixedDelay(operationContext, page, frame),
            OperationKind.Invocation);
    }

    private static void AnalyzePlaywrightFixedDelay(
        OperationAnalysisContext context,
        INamedTypeSymbol? page,
        INamedTypeSymbol? frame)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name != "WaitForTimeoutAsync"
            || (!SymbolEqualityComparer.Default.Equals(method.ContainingType, page)
                && !SymbolEqualityComparer.Default.Equals(method.ContainingType, frame)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            PlaywrightFixedDelayRule,
            invocation.Syntax.GetLocation(),
            method.ContainingType.Name));
    }
}
