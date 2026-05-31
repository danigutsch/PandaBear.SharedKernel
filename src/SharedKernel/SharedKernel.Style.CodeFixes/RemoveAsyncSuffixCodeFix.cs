using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
namespace SharedKernel.Style.CodeFixes;

internal static class RemoveAsyncSuffixCodeFix
{
    private const string AsyncSuffix = "Async";

    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        if (semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !methodSymbol.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
        {
            return;
        }

        var updatedName = methodSymbol.Name.Substring(0, methodSymbol.Name.Length - AsyncSuffix.Length);
        if (string.IsNullOrWhiteSpace(updatedName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename to '{updatedName}'",
                createChangedSolution: cancellationToken => RenameMethod(context.Document.Project.Solution, methodSymbol, updatedName, cancellationToken),
                equivalenceKey: $"RenameMethod:{updatedName}"),
            diagnostic);
    }

    private static Task<Solution> RenameMethod(
        Solution solution,
        ISymbol methodSymbol,
        string updatedName,
        CancellationToken cancellationToken)
    {
        return Renamer.RenameSymbolAsync(solution, methodSymbol, new SymbolRenameOptions(), updatedName, cancellationToken);
    }
}
