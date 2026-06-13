using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the rename-based code fix for <c>SKSTYLE002</c> diagnostics.
/// </summary>
internal static class RenameCancellationTokenParameterCodeFix
{
    private const string CanonicalParameterName = "ct";

    /// <summary>
    /// Registers a rename code action when the diagnostic targets a parameter declaration.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        var parameterSyntax = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ParameterSyntax>();
        if (parameterSyntax is null)
        {
            return;
        }

        if (semanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken) is not IParameterSymbol parameterSymbol
            || string.Equals(parameterSymbol.Name, CanonicalParameterName, StringComparison.Ordinal)
            || HasRenameConflict(parameterSyntax))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Rename to 'ct'",
                createChangedSolution: cancellationToken => Renamer.RenameSymbolAsync(
                    context.Document.Project.Solution,
                    parameterSymbol,
                    new SymbolRenameOptions(),
                    CanonicalParameterName,
                    cancellationToken),
                equivalenceKey: "RenameCancellationTokenParameter:ct"),
            diagnostic);
    }

    private static bool HasRenameConflict(ParameterSyntax parameterSyntax)
    {
        var siblings = parameterSyntax.Parent?.ChildNodes().OfType<ParameterSyntax>() ?? [];

        return siblings.Any(candidate =>
            candidate != parameterSyntax
            && string.Equals(candidate.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal));
    }
}
