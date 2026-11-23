using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using PandaBear.Aspire.Analyzers;

namespace PandaBear.Aspire.CodeFixes;

/// <summary>
/// Registers placeholder fixes for <c>SKASPIRE001</c> diagnostics.
/// </summary>
internal static class AddAspireImagePinPlaceholderCodeFix
{
    private const string WithImageTagMethodName = "WithImageTag";
    private const string WithImageSha256MethodName = "WithImageSHA256";
    private const string ImageTagPlaceholder = "REPLACE_WITH_VERIFIED_IMAGE_TAG";
    private const string ImageDigestPlaceholder = "REPLACE_WITH_VERIFIED_SHA256_DIGEST";
    private const string Sha256Prefix = "sha256:";

    /// <summary>
    /// Registers a placeholder code action for an Aspire image pin diagnostic.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var invocation = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        if (IsWithPrefixedDigest(invocation))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove sha256: prefix from verified digest",
                    createChangedDocument: _ => RemoveDigestPrefix(context.Document, root, invocation),
                    equivalenceKey: "RemoveAspireImageDigestPrefix"),
                diagnostic);
        }

        var outermostInvocation = InvocationChain.Outermost(invocation);
        var chain = InvocationChain.Links(outermostInvocation).ToArray();
        var hasImageTag = chain.Any(static candidate =>
            string.Equals(InvocationChain.MethodName(candidate), WithImageTagMethodName, StringComparison.Ordinal));
        var hasImageSha256 = chain.Any(static candidate =>
            string.Equals(InvocationChain.MethodName(candidate), WithImageSha256MethodName, StringComparison.Ordinal));

        if (!hasImageTag)
        {
            RegisterAppendPlaceholder(
                context,
                diagnostic,
                root,
                outermostInvocation,
                WithImageTagMethodName,
                ImageTagPlaceholder,
                "Insert placeholder to replace with verified image tag");
        }

        if (!hasImageSha256)
        {
            RegisterAppendPlaceholder(
                context,
                diagnostic,
                root,
                outermostInvocation,
                WithImageSha256MethodName,
                ImageDigestPlaceholder,
                "Insert placeholder to replace with verified bare SHA-256 digest");
        }
    }

    private static void RegisterAppendPlaceholder(
        CodeFixContext context,
        Diagnostic diagnostic,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string methodName,
        string placeholder,
        string title)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: _ => AppendPlaceholderCall(context.Document, root, invocation, methodName, placeholder),
                equivalenceKey: $"AddAspireImagePinPlaceholder:{methodName}"),
            diagnostic);
    }

    private static Task<Document> RemoveDigestPrefix(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.First();
        var literal = (LiteralExpressionSyntax)argument.Expression;
        var digest = literal.Token.ValueText.Substring(Sha256Prefix.Length);
        var updatedArgument = argument
            .WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(digest)))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = root.ReplaceNode(argument, updatedArgument);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static Task<Document> AppendPlaceholderCall(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string methodName,
        string placeholder)
    {
        var updatedInvocation = SyntaxFactory.ParseExpression($"{invocation}.{methodName}({placeholder})")
            .WithLeadingTrivia(invocation.GetLeadingTrivia())
            .WithTrailingTrivia(invocation.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = root.ReplaceNode(invocation, updatedInvocation);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static bool IsWithPrefixedDigest(InvocationExpressionSyntax invocation)
    {
        return string.Equals(InvocationChain.MethodName(invocation), WithImageSha256MethodName, StringComparison.Ordinal)
            && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.ValueText.StartsWith(Sha256Prefix, StringComparison.Ordinal);
    }
}
