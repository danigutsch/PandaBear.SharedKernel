using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the cooperative cancellation catch-filter code fix.
/// </summary>
internal static class UseShouldHandleAsFailureCodeFix
{
    private const string SharedKernelBuildingBlocksNamespace = "SharedKernel.BuildingBlocks";
    private const string CancellationTokenParameterName = "ct";

    /// <summary>
    /// Registers a code action when the diagnostic targets a catch filter expression.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var filter = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<CatchFilterClauseSyntax>();
        if (filter?.Parent is not CatchClauseSyntax { Declaration.Identifier.ValueText: { Length: > 0 } exceptionName })
        {
            return;
        }

        if (!HasCancellationTokenNamedCtInScope(filter))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use ShouldHandleAsFailure(ct)",
                createChangedDocument: _ => Apply(context.Document, root, filter, exceptionName),
                equivalenceKey: "UseShouldHandleAsFailure"),
            diagnostic);
    }

    private static Task<Document> Apply(
        Document document,
        SyntaxNode root,
        CatchFilterClauseSyntax filter,
        string exceptionName)
    {
        var replacement = SyntaxFactory.ParseExpression($"{exceptionName}.ShouldHandleAsFailure(ct)")
            .WithTriviaFrom(filter.FilterExpression);
        var updatedRoot = root.ReplaceNode(filter.FilterExpression, replacement);
        updatedRoot = AddUsingIfMissing(updatedRoot);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static SyntaxNode AddUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit
            || compilationUnit.Usings.Any(static directive =>
                string.Equals(directive.Name?.ToString(), SharedKernelBuildingBlocksNamespace, StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(SharedKernelBuildingBlocksNamespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        return compilationUnit.WithUsings(compilationUnit.Usings.Add(usingDirective));
    }

    private static bool HasCancellationTokenNamedCtInScope(SyntaxNode node)
    {
        return node.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault()?.ParameterList.Parameters.Any(static parameter =>
                string.Equals(parameter.Identifier.ValueText, CancellationTokenParameterName, StringComparison.Ordinal)) == true
            || node.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault()?.ParameterList.Parameters.Any(static parameter =>
                string.Equals(parameter.Identifier.ValueText, CancellationTokenParameterName, StringComparison.Ordinal)) == true
            || node.Ancestors().OfType<ParenthesizedLambdaExpressionSyntax>().FirstOrDefault()?.ParameterList.Parameters.Any(static parameter =>
                string.Equals(parameter.Identifier.ValueText, CancellationTokenParameterName, StringComparison.Ordinal)) == true
            || string.Equals(
                node.Ancestors().OfType<SimpleLambdaExpressionSyntax>().FirstOrDefault()?.Parameter.Identifier.ValueText,
                CancellationTokenParameterName,
                StringComparison.Ordinal);
    }
}
