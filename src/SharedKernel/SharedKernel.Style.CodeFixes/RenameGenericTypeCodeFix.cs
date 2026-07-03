using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the rename-based code fix for <c>SKSTYLE005</c> diagnostics.
/// </summary>
internal static class RenameGenericTypeCodeFix
{
    private static readonly ImmutableArray<string> GenericTypeNameSuffixes = ["Gereric", "Generic", "OfT"];

    /// <summary>
    /// Registers a rename code action when the diagnostic targets a generic type declaration.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        var targetNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var typeSymbol = GetTypeSymbol(semanticModel, targetNode, context.CancellationToken);
        if (typeSymbol is null || typeSymbol.Arity == 0)
        {
            return;
        }

        var updatedName = GetUpdatedName(typeSymbol.Name);
        if (updatedName is null
            || string.IsNullOrWhiteSpace(updatedName)
            || HasRenameConflict(typeSymbol, updatedName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename to '{updatedName}'",
                createChangedSolution: cancellationToken => Renamer.RenameSymbolAsync(
                    context.Document.Project.Solution,
                    typeSymbol,
                    new SymbolRenameOptions(),
                    updatedName,
                    cancellationToken),
                equivalenceKey: $"RenameGenericType:{updatedName}"),
            diagnostic);
    }

    private static INamedTypeSymbol? GetTypeSymbol(
        SemanticModel semanticModel,
        SyntaxNode targetNode,
        CancellationToken cancellationToken)
    {
        if (targetNode.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDeclaration)
        {
            return (INamedTypeSymbol?)semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken);
        }

        if (targetNode.FirstAncestorOrSelf<DelegateDeclarationSyntax>() is { } delegateDeclaration)
        {
            return (INamedTypeSymbol?)semanticModel.GetDeclaredSymbol(delegateDeclaration, cancellationToken);
        }

        return null;
    }

    private static string? GetUpdatedName(string typeName)
    {
        var suffix = GenericTypeNameSuffixes.FirstOrDefault(suffix =>
            typeName.Length > suffix.Length
            && typeName.EndsWith(suffix, StringComparison.Ordinal));

        return suffix is null ? null : typeName.Substring(0, typeName.Length - suffix.Length);
    }

    private static bool HasRenameConflict(INamedTypeSymbol typeSymbol, string updatedName)
    {
        return typeSymbol.ContainingType is null
            ? typeSymbol.ContainingNamespace.GetTypeMembers(updatedName, typeSymbol.Arity)
                .Any(candidate => !SymbolEqualityComparer.Default.Equals(candidate, typeSymbol))
            : typeSymbol.ContainingType.GetTypeMembers(updatedName, typeSymbol.Arity)
                .Any(candidate => !SymbolEqualityComparer.Default.Equals(candidate, typeSymbol));
    }
}
