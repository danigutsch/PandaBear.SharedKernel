using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Forwards the available handler cancellation token to mediator dispatch calls.
/// </summary>
internal static class MissingCancellationForwardingCodeFix
{
    /// <summary>
    /// Registers the cancellation-forwarding code fix when the diagnostic targets a dispatch invocation.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The missing-cancellation-forwarding diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        if (!TryCreatePlan(root, semanticModel, diagnostic, context.CancellationToken, out var invocation, out var plan))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Forward CancellationToken {plan.AvailableCancellationTokenName}",
                createChangedDocument: cancellationToken => ForwardCancellationTokenAsync(context.Document, invocation, plan, cancellationToken),
                equivalenceKey: "ForwardMediatorCancellationToken"),
            diagnostic);
    }

    private static async Task<Document> ForwardCancellationTokenAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ForwardingPlan plan,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var ctExpression = SyntaxFactory.IdentifierName(plan.AvailableCancellationTokenName);
        var updatedArgumentList = invocation.ArgumentList;
        if (plan.ExistingArgumentIndex >= 0)
        {
            var existingArgument = invocation.ArgumentList.Arguments[plan.ExistingArgumentIndex];
            updatedArgumentList = updatedArgumentList.ReplaceNode(
                existingArgument,
                existingArgument.WithExpression(ctExpression));
        }
        else
        {
            var argument = plan.UseNamedArgument
                ? SyntaxFactory.Argument(ctExpression)
                    .WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(plan.CancellationParameterName)))
                : SyntaxFactory.Argument(ctExpression);
            updatedArgumentList = updatedArgumentList.AddArguments(argument);
        }

        var updatedInvocation = invocation
            .WithArgumentList(updatedArgumentList)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = root.ReplaceNode(invocation, updatedInvocation);

        return document.WithSyntaxRoot(updatedRoot);
    }

    private static bool TryCreatePlan(
        SyntaxNode root,
        SemanticModel semanticModel,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax invocation,
        out ForwardingPlan plan)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var invocationSyntax = node.FirstAncestorOrSelf<InvocationExpressionSyntax>() ?? node as InvocationExpressionSyntax;
        if (invocationSyntax is null)
        {
            invocation = null!;
            plan = default;
            return false;
        }

        invocation = invocationSyntax;

        var containingMethod = semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken) as IMethodSymbol;
        var availableCancellationToken = containingMethod?.Parameters.FirstOrDefault(
            parameter => string.Equals(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), $"global::{MetadataNames.CancellationToken}", StringComparison.Ordinal));
        if (availableCancellationToken is null)
        {
            plan = default;
            return false;
        }

        var operation = semanticModel.GetOperation(invocation, cancellationToken) as Microsoft.CodeAnalysis.Operations.IInvocationOperation;
        var targetMethod = operation?.TargetMethod ?? GetCandidateTargetMethod(semanticModel, invocation, cancellationToken);
        if (targetMethod is null || !IsMediatorDispatchMethod(semanticModel.Compilation, targetMethod))
        {
            plan = default;
            return false;
        }

        var existingArgumentIndex = -1;
        if (operation is not null)
        {
            for (var index = 0; index < operation.Arguments.Length; index++)
            {
                if (SymbolEqualityComparer.Default.Equals(operation.Arguments[index].Parameter?.Type, availableCancellationToken.Type))
                {
                    existingArgumentIndex = index;
                    break;
                }
            }
        }

        var useNamedArgument = existingArgumentIndex < 0 && invocation.ArgumentList.Arguments.Any(static argument => argument.NameColon is not null);
        var cancellationParameter = targetMethod.Parameters.FirstOrDefault(
            parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, availableCancellationToken.Type));
        if (cancellationParameter is null)
        {
            plan = default;
            return false;
        }

        plan = new ForwardingPlan(availableCancellationToken.Name, cancellationParameter.Name, existingArgumentIndex, useNamedArgument);
        return true;
    }

    private static IMethodSymbol? GetCandidateTargetMethod(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            return method;
        }

        return symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
    }

    private static bool IsMediatorDispatchMethod(Compilation compilation, IMethodSymbol method)
    {
        return string.Equals(method.Name, "Send", StringComparison.Ordinal)
               && TypeImplementsOrEquals(compilation, method.ContainingType, MetadataNames.Sender)
               || string.Equals(method.Name, "Publish", StringComparison.Ordinal)
               && TypeImplementsOrEquals(compilation, method.ContainingType, MetadataNames.Publisher);
    }

    private static bool TypeImplementsOrEquals(Compilation compilation, INamedTypeSymbol type, string metadataName)
    {
        var contract = compilation.GetTypeByMetadataName(metadataName);
        return contract is not null
               && (SymbolEqualityComparer.Default.Equals(type, contract)
                   || SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, contract)
                   || type.AllInterfaces.Any(
                       candidate => SymbolEqualityComparer.Default.Equals(candidate, contract)
                                    || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract)));
    }

    private readonly struct ForwardingPlan(
        string availableCancellationTokenName,
        string cancellationParameterName,
        int existingArgumentIndex,
        bool useNamedArgument)
    {
        public string AvailableCancellationTokenName { get; } = availableCancellationTokenName;

        public string CancellationParameterName { get; } = cancellationParameterName;

        public int ExistingArgumentIndex { get; } = existingArgumentIndex;

        public bool UseNamedArgument { get; } = useNamedArgument;
    }
}
