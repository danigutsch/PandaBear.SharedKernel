using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using PandaBear.Testing.Analyzers;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PandaBear.Testing.CodeFixes;

public sealed partial class SharedKernelTestingCodeFixProvider
{
    private static async Task RegisterSafeInvocationFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        SyntaxNode root)
    {
        var invocation = root.FindNode(context.Span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (diagnostic.Id == TestingDiagnosticIds.ConditionalAssertion)
        {
            invocation ??= root.FindNode(context.Span).DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().LastOrDefault();
        }

        var model = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (invocation is null || model?.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var annotation = new SyntaxAnnotation();
        SyntaxNode? updatedRoot = null;
        if (diagnostic.Id == TestingDiagnosticIds.LinqSingleAssertion
            && method.Name == "Single"
            && method.ContainingType.ToDisplayString() is "System.Linq.Enumerable" or "System.Linq.Queryable"
            && invocation.ArgumentList.Arguments.All(static argument => argument.NameColon is null))
        {
            ExpressionSyntax? receiver;
            var arguments = invocation.ArgumentList.Arguments;
            if (method.ReducedFrom is not null && invocation.Expression is MemberAccessExpressionSyntax member)
            {
                receiver = member.Expression;
            }
            else
            {
                receiver = arguments.FirstOrDefault()?.Expression;
                arguments = arguments.Count > 0 ? arguments.RemoveAt(0) : arguments;
            }

            if (receiver is null || receiver is ParenthesizedExpressionSyntax || arguments.Count > 1)
            {
                return;
            }

            if (arguments.Count == 1 && arguments[0].Expression is not LambdaExpressionSyntax)
            {
                // A delegate method group captures the original Func once; invoking a captured
                // variable in a new lambda could observe subsequent reassignment during enumeration.
                var predicate = arguments[0].Expression;
                if (predicate is IdentifierNameSyntax
                    && model.GetTypeInfo(predicate, context.CancellationToken).Type?.TypeKind == TypeKind.Delegate)
                {
                    arguments = SingletonSeparatedList(Argument(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, predicate, IdentifierName("Invoke"))));
                }
            }

            var replacement = InvocationExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, receiver.WithoutTrivia(), IdentifierName("ShouldHaveSingleItem")))
                .WithArgumentList(ArgumentList(arguments))
                .WithTriviaFrom(invocation)
                .WithAdditionalAnnotations(annotation, Formatter.Annotation);
            updatedRoot = AddAssertionUsing(root.ReplaceNode(invocation, replacement));
        }
        else if (diagnostic.Id == TestingDiagnosticIds.ConditionalAssertion
            && method.ContainingType.ToDisplayString() == "PandaBear.Testing.Assertions.ShouldAssertionExtensions"
            && method.Name.StartsWith("ShouldBe", StringComparison.Ordinal)
            && invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>() is { Parent: BlockSyntax block } statement
            && statement.Expression is ConditionalAccessExpressionSyntax conditional
            && conditional.Span.Contains(invocation.Span))
        {
            // Guarding a projected value must not turn an intentional null expectation into
            // an assertion which can never pass. These cases need an explicit parent guard.
            if (method.Name == "ShouldBeNull")
            {
                return;
            }

            if (method.Name == "ShouldBe" && invocation.ArgumentList.Arguments.FirstOrDefault() is { } expected)
            {
                var constant = model.GetConstantValue(expected.Expression, context.CancellationToken);
                var expectedType = model.GetTypeInfo(expected.Expression, context.CancellationToken);
                if (constant.HasValue && constant.Value is null
                    || !constant.HasValue && expectedType.Nullability.FlowState != NullableFlowState.NotNull
                        && (expectedType.Type?.IsReferenceType == true
                            || expectedType.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }))
                {
                    return;
                }
            }

            var name = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name,
                MemberBindingExpressionSyntax binding => binding.Name,
                _ => null,
            };
            if (name is null)
            {
                return;
            }

            ExpressionSyntax receiver;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                receiver = conditional.ReplaceNode(invocation, memberAccess.Expression);
            }
            else if (invocation.Parent is ConditionalAccessExpressionSyntax directConditional)
            {
                receiver = directConditional == conditional
                    ? directConditional.Expression
                    : conditional.ReplaceNode(directConditional, directConditional.Expression);
            }
            else
            {
                return;
            }

            var localName = "assertionTarget";
            var suffix = 0;
            while (model.LookupSymbols(statement.SpanStart, name: localName).Any()
                || block.DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == localName))
            {
                localName = "assertionTarget" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var declaration = LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator(localName).WithInitializer(EqualsValueClause(receiver.WithoutTrivia()))))
                .WithLeadingTrivia(statement.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
            var nonNullReceiver = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(localName), IdentifierName("ShouldNotBeNull")));
            var replacement = invocation.WithExpression(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, nonNullReceiver, name))
                .WithAdditionalAnnotations(annotation, Formatter.Annotation);
            var replacementStatement = statement.WithExpression(replacement)
                .WithLeadingTrivia(default(SyntaxTriviaList)).WithAdditionalAnnotations(Formatter.Annotation);
            var index = block.Statements.IndexOf(statement);
            updatedRoot = AddAssertionUsing(root.ReplaceNode(block, block.WithStatements(
                block.Statements.RemoveAt(index).InsertRange(index, [declaration, replacementStatement]))));
        }

        if (updatedRoot is null)
        {
            return;
        }

        var updatedDocument = document.WithSyntaxRoot(updatedRoot);
        var updatedModel = await updatedDocument.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var updatedTree = await updatedDocument.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var updatedInvocation = updatedTree?.GetAnnotatedNodes(annotation).OfType<InvocationExpressionSyntax>().SingleOrDefault();
        if (updatedInvocation is null || updatedModel is null
            || updatedModel.GetSymbolInfo(updatedInvocation, context.CancellationToken).Symbol is not IMethodSymbol replacementMethod
            || replacementMethod.ContainingType.ToDisplayString() != "PandaBear.Testing.Assertions.ShouldAssertionExtensions"
            || updatedModel.GetDiagnostics(updatedInvocation.Span, context.CancellationToken).Any(static d => d.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create(
            diagnostic.Id == TestingDiagnosticIds.LinqSingleAssertion ? "Use ShouldHaveSingleItem" : "Assert the nullable target explicitly",
            _ => Task.FromResult(updatedDocument), equivalenceKey: diagnostic.Id), diagnostic);
    }
}
