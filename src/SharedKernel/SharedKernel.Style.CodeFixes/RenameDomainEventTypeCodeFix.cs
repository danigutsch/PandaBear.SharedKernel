using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace SharedKernel.Style.CodeFixes;

internal static class RenameDomainEventTypeCodeFix
{
    private const string DomainEventSuffix = "DomainEvent";

    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        var targetNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (targetNode.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not TypeDeclarationSyntax typeDeclaration
            || semanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not INamedTypeSymbol typeSymbol
            || typeSymbol.ContainingType is not null)
        {
            return;
        }

        var updatedName = typeSymbol.Name + DomainEventSuffix;
        if (HasRenameConflict(typeSymbol, updatedName))
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
                equivalenceKey: $"RenameDomainEventType:{updatedName}"),
            diagnostic);
    }

    private static bool HasRenameConflict(INamedTypeSymbol typeSymbol, string updatedName)
    {
        return typeSymbol.ContainingNamespace.GetTypeMembers(updatedName, typeSymbol.Arity)
            .Any(candidate => !SymbolEqualityComparer.Default.Equals(candidate, typeSymbol));
    }
}
