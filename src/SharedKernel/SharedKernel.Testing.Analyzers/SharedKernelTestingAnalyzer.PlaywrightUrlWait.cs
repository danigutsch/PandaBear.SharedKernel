using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private const string PlaywrightUrlWaitMethodName = "WaitForURLAsync";

    private static readonly DiagnosticDescriptor PlaywrightUrlWaitRule = new(
        TestingDiagnosticIds.PlaywrightUrlWait,
        "Wait for a page address with ExpectPath",
        "Use ExpectPath instead of {0}.WaitForURLAsync, which loses a navigation that lands between its address check and its event subscription",
        TestingCategory, DiagnosticSeverity.Warning, true,
        description: "Playwright .NET's WaitForURLAsync reads the current address and only then subscribes to navigation events, which arrive on another thread; a navigation in between is lost and the wait times out (microsoft/playwright-dotnet#3364). ExpectPath polls the address in the driver instead.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest020");

    private static void RegisterPlaywrightUrlWaitAnalysis(CompilationStartAnalysisContext compilationContext)
    {
        var page = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Playwright.IPage");
        var frame = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Playwright.IFrame");
        if (page is null && frame is null)
        {
            return;
        }

        compilationContext.RegisterOperationAction(
            operationContext => AnalyzePlaywrightUrlWait(operationContext, page, frame),
            OperationKind.Invocation);
    }

    private static void AnalyzePlaywrightUrlWait(OperationAnalysisContext context, INamedTypeSymbol? page, INamedTypeSymbol? frame)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name != PlaywrightUrlWaitMethodName)
        {
            return;
        }

        var owner = method.ContainingType;
        if (!SymbolEqualityComparer.Default.Equals(owner, page) && !SymbolEqualityComparer.Default.Equals(owner, frame))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(PlaywrightUrlWaitRule, invocation.Syntax.GetLocation(), owner.Name));
    }
}
