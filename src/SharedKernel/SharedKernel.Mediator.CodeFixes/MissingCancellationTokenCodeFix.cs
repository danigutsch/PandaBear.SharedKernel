using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Adds the missing <c>CancellationToken ct</c> parameter to a public handler method.
/// </summary>
internal static class MissingCancellationTokenCodeFix
{
    /// <summary>
    /// Registers the missing-parameter code fix when the diagnostic targets a compatible handler method.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The missing-cancellation-token diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null
            || !string.Equals(methodDeclaration.Identifier.ValueText, "Handle", StringComparison.Ordinal)
            || methodDeclaration.ParameterList.Parameters.Count != 1)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add CancellationToken ct parameter",
                createChangedDocument: cancellationToken => AddCancellationTokenParameterAsync(context.Document, methodDeclaration, cancellationToken),
                equivalenceKey: "AddMediatorCancellationTokenParameter"),
            diagnostic);
    }

    private static async Task<Document> AddCancellationTokenParameterAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"));
        var updatedMethod = methodDeclaration
            .WithParameterList(methodDeclaration.ParameterList.AddParameters(parameter))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = root.ReplaceNode(methodDeclaration, updatedMethod);

        return document.WithSyntaxRoot(updatedRoot);
    }
}
