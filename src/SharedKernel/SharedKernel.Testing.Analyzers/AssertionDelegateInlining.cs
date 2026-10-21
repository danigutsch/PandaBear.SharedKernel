using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PandaBear.Testing.Analyzers;

/// <summary>
/// Shares conservative delegate rewrite validation between the diagnostic and its code fix.
/// </summary>
internal static class AssertionDelegateInlining
{
    public static bool TryRewrite(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        out LocalDeclarationStatementSyntax? declaration,
        out InvocationExpressionSyntax? replacement)
    {
        declaration = null;
        replacement = null;
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };
        if (name is null || name.Identifier.ValueText is not ("ShouldThrow" or "ShouldThrowAssignableTo"))
        {
            return false;
        }

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingAssembly.Identity.Name != "SharedKernel.Testing.Assertions"
            || method.ContainingType.ToDisplayString() != "PandaBear.Testing.Assertions.ShouldAssertionExtensions"
            || method.Name is not ("ShouldThrow" or "ShouldThrowAssignableTo")
            || invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } testMethod
            || model.GetDeclaredSymbol(testMethod, cancellationToken) is not IMethodSymbol testSymbol
            || !testSymbol.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() is "Xunit.FactAttribute" or "Xunit.TheoryAttribute"))
        {
            return false;
        }

        var original = method.ReducedFrom is { } reduced
            ? reduced.ConstructedFrom.Construct(method.TypeArguments.ToArray())
            : method;
        var delegateType = original.Parameters[0].Type.ToDisplayString();
        if (delegateType is not ("System.Action" or "System.Func<object>" or "System.Func<object?>" or "System.Func<System.Threading.Tasks.Task>"))
        {
            return false;
        }

        IdentifierNameSyntax? identifier;
        if (method.ReducedFrom is not null)
        {
            identifier = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression as IdentifierNameSyntax;
        }
        else
        {
            identifier = invocation.ArgumentList.Arguments
                .FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == original.Parameters[0].Name
                    || (a.NameColon is null && invocation.ArgumentList.Arguments.IndexOf(a) == 0))?.Expression as IdentifierNameSyntax;
        }

        if (identifier is null
            || model.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax { Initializer.Value: LambdaExpressionSyntax lambda } variable
            || variable.Parent is not VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax localDeclaration }
            || !SymbolEqualityComparer.Default.Equals(local.Type, original.Parameters[0].Type)
            || localDeclaration.UsingKeyword != default
            || localDeclaration.Modifiers.Count != 0
            || localDeclaration.Parent is not BlockSyntax block
            || invocation.FirstAncestorOrSelf<StatementSyntax>() is not { } assertionStatement
            || block.Statements.IndexOf(localDeclaration) + 1 != block.Statements.IndexOf(assertionStatement)
            || localDeclaration.ContainsDirectives
            || assertionStatement.ContainsDirectives
            || localDeclaration.DescendantTrivia().Any(trivia => localDeclaration.Span.Contains(trivia.Span) && !lambda.Span.Contains(trivia.Span)
                && (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)))
            || invocation.Expression.DescendantTrivia().Any(trivia => invocation.Span.Contains(trivia.Span)
                && (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))))
        {
            return false;
        }

        // Do not move the lambda into a new scope that could rebind captured member names.
        ExpressionSyntax assertionValue = invocation.Parent is AwaitExpressionSyntax awaited ? awaited : invocation;
        if (assertionValue.Parent is not (ExpressionStatementSyntax or ReturnStatementSyntax or EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax
                {
                    Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax },
                },
            }))
        {
            return false;
        }

        // Inspect the entire method, including closures and nested scopes, for reads and writes.
        var references = testMethod.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == local.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, local));
        if (references.Count() != 1)
        {
            return false;
        }

        var arguments = invocation.ArgumentList;
        if (method.ReducedFrom is not null)
        {
            arguments = arguments.WithArguments(arguments.Arguments.Insert(0, SyntaxFactory.Argument(lambda.WithoutTrivia())));
        }
        else
        {
            arguments = arguments.ReplaceNode(identifier, lambda.WithTriviaFrom(identifier));
        }

        var candidate = invocation.WithExpression(SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseName("global::PandaBear.Testing.Assertions.ShouldAssertionExtensions"),
            name.WithoutTrivia())).WithArgumentList(arguments);
        if (model.GetSpeculativeSymbolInfo(invocation.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression).Symbol is not IMethodSymbol rebound
            || !SymbolEqualityComparer.Default.Equals(original, rebound))
        {
            return false;
        }

        declaration = localDeclaration;
        replacement = candidate.WithTriviaFrom(invocation);
        return true;
    }
}
