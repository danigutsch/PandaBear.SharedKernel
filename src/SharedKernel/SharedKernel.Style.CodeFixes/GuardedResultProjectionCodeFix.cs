using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SharedKernel.Style.Analyzers;

namespace SharedKernel.Style.CodeFixes;

/// <summary>Replaces a verified success projection guard with a Result.Map invocation.</summary>
internal static class GuardedResultProjectionCodeFix
{
    /// <summary>Offers the rewrite when the current document still has a verified match.</summary>
    internal static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var ct = context.CancellationToken;
        var document = context.Document;
        var position = diagnostic.Location.SourceSpan.Start;
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root is null || model is null || Find(root, model, position, ct) is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create("Use Result.Map", cancellationToken => Apply(document, position, cancellationToken),
                equivalenceKey: StyleDiagnosticIds.GuardedResultProjection),
            diagnostic);
    }

    private static async Task<Document> Apply(Document document, int position, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root is null || model is null || Find(root, model, position, ct) is not { } match
            || match.Guard.Parent is not BlockSyntax block)
        {
            return document;
        }

        var replacement = match.Success.WithExpression(match.Map)
            .WithLeadingTrivia(match.Guard.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        var index = block.Statements.IndexOf(match.Guard);
        var statements = SyntaxFactory.List(block.Statements.Take(index).Concat([replacement]));
        return document.WithSyntaxRoot(root.ReplaceNode(block, block.WithStatements(statements)));
    }

    private static ResultProjectionMatch? Find(SyntaxNode root, SemanticModel model, int position, CancellationToken ct) =>
        root.FullSpan.Contains(position)
        && root.FindToken(position).Parent?.FirstAncestorOrSelf<IfStatementSyntax>() is { } guard
        && guard.IfKeyword.SpanStart == position
            ? ResultProjectionMatch.Find(guard, model, ct)
            : null;
}
