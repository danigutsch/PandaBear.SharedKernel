using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the rename-based code fix for <c>SKSTYLE001</c> diagnostics.
/// </summary>
internal static class RemoveAsyncSuffixCodeFix
{
    private const string AsyncSuffix = "Async";

    /// <summary>
    /// Registers a rename code action when the diagnostic targets a method declaration.
    /// </summary>
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
        if (string.IsNullOrWhiteSpace(updatedName)
            || HasRenameConflict(methodSymbol, updatedName))
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

    private static bool HasRenameConflict(IMethodSymbol methodSymbol, string updatedName)
    {
        for (INamedTypeSymbol? containingType = methodSymbol.ContainingType; containingType is not null; containingType = containingType.BaseType)
        {
            if (containingType
                .GetMembers(updatedName)
                .OfType<IMethodSymbol>()
                .Any(candidate =>
                    !SymbolEqualityComparer.Default.Equals(candidate, methodSymbol)
                    && candidate.MethodKind == methodSymbol.MethodKind
                    && candidate.Parameters.Length == methodSymbol.Parameters.Length
                    && candidate.TypeParameters.Length == methodSymbol.TypeParameters.Length
                    && candidate.Arity == methodSymbol.Arity
                    && ParametersMatch(candidate.Parameters, methodSymbol.Parameters)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParametersMatch(ImmutableArray<IParameterSymbol> left, ImmutableArray<IParameterSymbol> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[index].Type, right[index].Type)
                || left[index].RefKind != right[index].RefKind)
            {
                return false;
            }
        }

        return true;
    }
}
