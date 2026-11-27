using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers;

public sealed partial class StyleAnalyzer
{
    private static readonly DiagnosticDescriptor GuardedResultProjectionRule = new(
        StyleDiagnosticIds.GuardedResultProjection,
        title: "Use Result.Map for a guarded success projection",
        messageFormat: "Use Result.Map to project this initialized result",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "A verified initialized result can project its success value while preserving the original failure.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle019");

    private static void AnalyzeGuardedResultProjection(SyntaxNodeAnalysisContext context)
    {
        var guard = (IfStatementSyntax)context.Node;
        if (ResultProjectionMatch.Find(guard, context.SemanticModel, context.CancellationToken) is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(GuardedResultProjectionRule, guard.IfKeyword.GetLocation()));
        }
    }
}
