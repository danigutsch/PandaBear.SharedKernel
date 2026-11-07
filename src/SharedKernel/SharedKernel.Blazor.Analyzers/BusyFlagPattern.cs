using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PandaBear.Blazor.Analyzers;

/// <summary>
/// Finds the busy-flag pattern in a block: <c>flag = true;</c>, one or more statements that await, then <c>flag = false;</c>
/// as a direct statement of the same block, so an exception between them leaves the flag set. An await inside the try block of a
/// try statement with a catch-all clause that does not rethrow is protected, because every path still reaches the reset. A catch-all that can throw, return, goto or break or continue out of
/// the block does not protect, since the reset is then skipped. Awaits inside nested lambdas, anonymous methods and local functions never count.
/// </summary>
internal static class BusyFlagPattern
{
    /// <summary>
    /// Looks for the reset of the flag set by the statement at <paramref name="setIndex"/> and checks that an await lies between them.
    /// </summary>
    /// <param name="semanticModel">The semantic model of the block.</param>
    /// <param name="block">The block that directly contains the statements.</param>
    /// <param name="setIndex">The index of the <c>flag = true;</c> statement in the block.</param>
    /// <param name="resetIndex">The index of the matching <c>flag = false;</c> statement, when the pattern is found.</param>
    /// <returns><see langword="true"/> when the pattern is found.</returns>
    public static bool TryFindReset(SemanticModel semanticModel, BlockSyntax block, int setIndex, out int resetIndex)
    {
        resetIndex = -1;
        var statements = block.Statements;
        var flag = GetFlag(semanticModel, statements[setIndex], expected: SyntaxKind.TrueLiteralExpression);
        if (flag is null)
        {
            return false;
        }

        var awaited = false;
        for (var index = setIndex + 1; index < statements.Count; index++)
        {
            var statement = statements[index];
            var resetFlag = GetFlag(semanticModel, statement, expected: SyntaxKind.FalseLiteralExpression);
            if (resetFlag is not null && SymbolEqualityComparer.Default.Equals(resetFlag, flag))
            {
                resetIndex = index;
                return awaited;
            }

            awaited |= ContainsUnprotectedAwait(semanticModel, statement);
        }

        return false;
    }

    /// <summary>
    /// Gets the assignment expression of a <c>flag = true;</c> statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The assignment, or <see langword="null"/> when the statement is something else.</returns>
    public static AssignmentExpressionSyntax? AsAssignment(StatementSyntax statement)
    {
        return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            ? assignment
            : null;
    }

    private static ISymbol? GetFlag(SemanticModel semanticModel, StatementSyntax statement, SyntaxKind expected)
    {
        var assignment = AsAssignment(statement);
        if (assignment is null || !assignment.Right.IsKind(expected))
        {
            return null;
        }

        var symbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
        return symbol is IFieldSymbol { Type.SpecialType: SpecialType.System_Boolean } or IPropertySymbol { Type.SpecialType: SpecialType.System_Boolean }
            ? symbol
            : null;
    }

    private static bool ContainsUnprotectedAwait(SemanticModel semanticModel, SyntaxNode node)
    {
        if (IsFunctionBoundary(node))
        {
            return false;
        }

        foreach (var child in node.ChildNodes())
        {
            if (IsFunctionBoundary(child))
            {
                continue;
            }

            // The try block of a protected try statement is skipped; its catch and finally parts still count.
            if (child is BlockSyntax { Parent: TryStatementSyntax tryStatement } tryBlock
                && tryStatement.Block == tryBlock
                && HasProtectingCatch(semanticModel, tryStatement))
            {
                continue;
            }

            if (IsAwait(child) || ContainsUnprotectedAwait(semanticModel, child))
            {
                return true;
            }
        }

        return IsAwait(node);
    }

    private static bool IsAwait(SyntaxNode node)
    {
        return node is AwaitExpressionSyntax
            or CommonForEachStatementSyntax { AwaitKeyword.RawKind: not 0 }
            or UsingStatementSyntax { AwaitKeyword.RawKind: not 0 }
            or LocalDeclarationStatementSyntax { AwaitKeyword.RawKind: not 0 };
    }

    private static bool HasProtectingCatch(SemanticModel semanticModel, TryStatementSyntax tryStatement)
    {
        foreach (var clause in tryStatement.Catches)
        {
            if (clause.Filter is null && IsCatchAll(semanticModel, clause) && FallsThrough(clause.Block))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFunctionBoundary(SyntaxNode node)
    {
        return node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;
    }

    /// <summary>
    /// Checks that the catch block cannot leave through a throw, return, goto, or a break or continue that targets a statement outside the block.
    /// Jumps inside nested lambdas, anonymous methods and local functions do not count.
    /// </summary>
    private static bool FallsThrough(BlockSyntax catchBlock)
    {
        foreach (var candidate in catchBlock.DescendantNodes(static node => !IsFunctionBoundary(node)))
        {
            if (candidate is ThrowStatementSyntax or ThrowExpressionSyntax or ReturnStatementSyntax or GotoStatementSyntax
                || (candidate is BreakStatementSyntax && !HasEnclosingTarget(candidate, catchBlock, breakable: true))
                || (candidate is ContinueStatementSyntax && !HasEnclosingTarget(candidate, catchBlock, breakable: false)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasEnclosingTarget(SyntaxNode jump, BlockSyntax catchBlock, bool breakable)
    {
        for (var ancestor = jump.Parent; ancestor is not null && ancestor != catchBlock; ancestor = ancestor.Parent)
        {
            if (ancestor is WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax
                || (breakable && ancestor is SwitchStatementSyntax))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCatchAll(SemanticModel semanticModel, CatchClauseSyntax clause)
    {
        if (clause.Declaration is null)
        {
            return true;
        }

        return semanticModel.GetTypeInfo(clause.Declaration.Type).Type is { SpecialType: SpecialType.System_Object }
            or { Name: "Exception", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };
    }
}
