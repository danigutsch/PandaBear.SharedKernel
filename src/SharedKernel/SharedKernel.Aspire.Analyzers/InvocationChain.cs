using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Aspire.Analyzers;

/// <summary>
/// Walks a fluent chain of invocations such as <c>builder.AddContainer(...).WithImageTag(...)</c>. The analyzer and the code fix
/// link this one source file, so both read a chain the same way.
/// </summary>
internal static class InvocationChain
{
    /// <summary>Gets the last invocation of the chain that <paramref name="invocation"/> belongs to.</summary>
    /// <param name="invocation">An invocation in the chain.</param>
    /// <returns>The outermost invocation.</returns>
    public static InvocationExpressionSyntax Outermost(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax memberAccess
            && ReferenceEquals(memberAccess.Expression, current)
            && memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            current = parentInvocation;
        }

        return current;
    }

    /// <summary>Gets the invocations of a chain from the first to <paramref name="invocation"/>.</summary>
    /// <param name="invocation">The last invocation to include.</param>
    /// <returns>The invocations in call order.</returns>
    public static IEnumerable<InvocationExpressionSyntax> Links(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is InvocationExpressionSyntax receiverInvocation)
        {
            foreach (var candidate in Links(receiverInvocation))
            {
                yield return candidate;
            }
        }

        yield return invocation;
    }

    /// <summary>Gets the name of the invoked method.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The method name, or <see langword="null"/> when the invocation names none.</returns>
    public static string? MethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => null
        };
    }
}
