using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
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

        if (siblings.Any(candidate =>
            candidate != parameterSyntax
            && string.Equals(candidate.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal)))
        {
            return true;
        }

        var containingScope = FindContainingConflictScope(parameterSyntax);
        if (containingScope is null)
        {
            return false;
        }

        return containingScope.DescendantNodes(static node => !IsNestedExecutableScope(node))
            .Any(node => DeclaresCanonicalName(node));
    }

    private static SyntaxNode? FindContainingConflictScope(ParameterSyntax parameterSyntax)
    {
        return parameterSyntax.Parent?.Parent switch
        {
            BaseMethodDeclarationSyntax method => method.Body ?? (SyntaxNode?)method.ExpressionBody,
            LocalFunctionStatementSyntax localFunction => localFunction.Body ?? (SyntaxNode?)localFunction.ExpressionBody,
            AnonymousFunctionExpressionSyntax anonymousFunction => anonymousFunction.Body,
            AccessorDeclarationSyntax accessor => accessor.Body ?? (SyntaxNode?)accessor.ExpressionBody,
            _ => null
        };
    }

    private static bool DeclaresCanonicalName(SyntaxNode node)
    {
        return node switch
        {
            VariableDeclaratorSyntax variable => string.Equals(variable.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal),
            SingleVariableDesignationSyntax designation => string.Equals(designation.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal),
            CatchDeclarationSyntax catchDeclaration => string.Equals(catchDeclaration.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal),
            ForEachStatementSyntax forEachStatement => string.Equals(forEachStatement.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal),
            LocalFunctionStatementSyntax localFunction => string.Equals(localFunction.Identifier.ValueText, CanonicalParameterName, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsNestedExecutableScope(SyntaxNode node)
    {
        return node is AnonymousFunctionExpressionSyntax
            or LocalFunctionStatementSyntax;
    }
}
