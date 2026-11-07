using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PandaBear.Blazor.CodeFixes;

/// <summary>
/// Registers the <c>SKBLZ002</c> fix that replaces an injected <c>ISender</c> with <c>ScopedSender</c>.
/// </summary>
/// <remarks>
/// There is no fix for an injected <c>DbContext</c>: the right replacement is an <c>IDbContextFactory</c> and the component code
/// that uses the context has to change with it. <c>ScopedSender</c> must be registered in the service collection.
/// </remarks>
internal static class UseScopedSenderCodeFix
{
    private const string Title = "Use ScopedSender";
    private const string EquivalenceKey = "UseScopedSender";

    /// <summary>
    /// Registers the fix for one <c>SKBLZ002</c> diagnostic when the injected type is <c>ISender</c>.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var node = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null
            || node is not TypeSyntax type
            || semanticModel?.GetSymbolInfo(type, cancellationToken).Symbol is not INamedTypeSymbol { Name: "ISender", ContainingNamespace: { Name: "Mediator", ContainingNamespace.Name: "PandaBear" } })
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, Replacement(type)))),
                EquivalenceKey),
            diagnostic);
    }

    private static TypeSyntax Replacement(TypeSyntax type)
    {
        var scopedSender = SyntaxFactory.IdentifierName("ScopedSender");
        return type is QualifiedNameSyntax qualified
            ? qualified.WithRight(scopedSender)
            : scopedSender.WithTriviaFrom(type);
    }
}
