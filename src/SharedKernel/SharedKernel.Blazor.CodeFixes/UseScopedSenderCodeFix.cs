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

        var compilation = await context.Document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var member = DeclaredMember(semanticModel, type, cancellationToken);
        if (compilation is null || member is null || !OnlySendsPlainRequests(compilation, member, cancellationToken))
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

    private static ISymbol? DeclaredMember(SemanticModel semanticModel, TypeSyntax type, CancellationToken cancellationToken)
    {
        return type.Parent switch
        {
            PropertyDeclarationSyntax property => semanticModel.GetDeclaredSymbol(property, cancellationToken),
            VariableDeclarationSyntax { Variables.Count: 1 } declaration => semanticModel.GetDeclaredSymbol(declaration.Variables[0], cancellationToken),
            _ => null
        };
    }

    // ScopedSender only has Send(IRequest<T>, ...). Every use of the injected member in the declaring type (all partial parts, so
    // razor-generated code is included) must be such a call; anything else (stream send, passing the sender on, assigning it)
    // would not compile against ScopedSender, so no fix is offered.
    private static bool OnlySendsPlainRequests(Compilation compilation, ISymbol member, CancellationToken cancellationToken)
    {
        foreach (var reference in member.ContainingType.DeclaringSyntaxReferences)
        {
            var tree = reference.SyntaxTree;
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(cancellationToken);
            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText == member.Name
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, member)
                    && !IsPlainSend(model, identifier, cancellationToken))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsPlainSend(SemanticModel model, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
    {
        // `this.Sender` is a member access whose name is the identifier; `Sender` alone is the expression itself.
        SyntaxNode use = identifier.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access && access.Name == identifier
            ? access
            : identifier;
        var invocation = FindSendInvocation(use);
        return invocation is not null
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: "Send", Parameters.Length: 2 } method
            && method.Parameters[0].Type is INamedTypeSymbol { Name: "IRequest", Arity: 1 };
    }

    private static InvocationExpressionSyntax? FindSendInvocation(SyntaxNode use)
    {
        if (use.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Send", Parent: InvocationExpressionSyntax call } member
            && member.Expression == use)
        {
            return call;
        }

        return use.Parent is ConditionalAccessExpressionSyntax conditional
            && conditional.Expression == use
            && conditional.WhenNotNull is InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Send" } } bound
                ? bound
                : null;
    }

    private static TypeSyntax Replacement(TypeSyntax type)
    {
        var scopedSender = SyntaxFactory.IdentifierName("ScopedSender");
        return type is QualifiedNameSyntax qualified
            ? qualified.WithRight(scopedSender)
            : scopedSender.WithTriviaFrom(type);
    }
}
