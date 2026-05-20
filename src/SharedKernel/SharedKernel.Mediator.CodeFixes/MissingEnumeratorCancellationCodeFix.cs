using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Adds <c>[EnumeratorCancellation]</c> to async stream handler and pipeline <c>Handle(...)</c> methods.
/// </summary>
internal static class MissingEnumeratorCancellationCodeFix
{
    /// <summary>
    /// Registers the missing-attribute code fix when the diagnostic targets a compatible
    /// <c>CancellationToken</c> parameter.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The missing-enumerator-cancellation diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var parameter = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter is null
            || parameter.Parent?.Parent is not MethodDeclarationSyntax methodDeclaration
            || !string.Equals(methodDeclaration.Identifier.ValueText, "Handle", StringComparison.Ordinal))
        {
            return;
        }

        if (parameter.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Any(static attribute => string.Equals(attribute.Name.ToString(), "EnumeratorCancellation", StringComparison.Ordinal)
                                     || string.Equals(
                                         attribute.Name.ToString(),
                                         "global::System.Runtime.CompilerServices.EnumeratorCancellation",
                                         StringComparison.Ordinal)
                                     || string.Equals(
                                         attribute.Name.ToString(),
                                         "global::System.Runtime.CompilerServices.EnumeratorCancellationAttribute",
                                         StringComparison.Ordinal)))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add [EnumeratorCancellation]",
                createChangedDocument: cancellationToken => AddEnumeratorCancellationAsync(context.Document, parameter, cancellationToken),
                equivalenceKey: "AddEnumeratorCancellation"),
            diagnostic);
    }

    private static async Task<Document> AddEnumeratorCancellationAsync(
        Document document,
        ParameterSyntax parameter,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("global::System.Runtime.CompilerServices.EnumeratorCancellation"));
        var updatedParameter = parameter
            .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = root.ReplaceNode(parameter, updatedParameter);

        return document.WithSyntaxRoot(updatedRoot);
    }
}
