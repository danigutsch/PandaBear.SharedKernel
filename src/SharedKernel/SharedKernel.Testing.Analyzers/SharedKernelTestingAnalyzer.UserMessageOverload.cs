using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private static readonly DiagnosticDescriptor AssertionUserMessageOverloadRule = new(
        TestingDiagnosticIds.AssertionUserMessageOverload,
        title: "Public assertions should have a userMessage overload",
        messageFormat: "Assertion '{0}' has no overload that takes 'string userMessage'{1}",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Assertion libraries give every public Should* extension method an overload that takes the reason the test expects the assertion to hold, after its other parameters and before a trailing CancellationToken. Enable this rule only in projects that declare assertions.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest017");

    // A partial method is reported once, at its definition; its implementation part is skipped.
    private static void AnalyzeAssertionUserMessageOverload(SymbolAnalysisContext context)
    {
        if (context.Symbol is not IMethodSymbol { PartialDefinitionPart: null } method
            || !AssertionUserMessageOverload.IsMissing(method, context.Compilation, out _, out var blocker)
            || method.Locations.FirstOrDefault(static location => location.IsInSource) is not { } location)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                AssertionUserMessageOverloadRule,
                location,
                method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                blocker is null ? string.Empty : ": " + blocker));
    }
}
