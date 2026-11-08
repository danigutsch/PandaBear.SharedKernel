using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using PandaBear.Blazor.Analyzers;

namespace PandaBear.Blazor.CodeFixes;

/// <summary>
/// Registers the <c>SKBLZ001</c> fix that wraps the statements between <c>flag = true;</c> and <c>flag = false;</c> in a
/// <c>try</c> block and moves the reset into its <c>finally</c> block.
/// </summary>
/// <remarks>
/// The fix is not offered when a local (including out, pattern and deconstruction variables) declared in the wrapped statements is used after the reset, because wrapping would move
/// the local out of scope.
/// </remarks>
internal static class ResetBusyFlagInFinallyCodeFix
{
    private const string Title = "Reset the flag in a finally block";
    private const string EquivalenceKey = "ResetBusyFlagInFinally";

    /// <summary>
    /// Registers the fix for one <c>SKBLZ001</c> diagnostic when it is safe.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var statement = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (root is null || statement?.Parent is not BlockSyntax block)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var setIndex = block.Statements.IndexOf(statement);
        if (semanticModel is null
            || setIndex < 0
            || !BusyFlagPattern.TryFindReset(semanticModel, block, setIndex, out var resetIndex)
            || DeclaredLocalsAreUsedAfter(block, setIndex + 1, resetIndex))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(block, Wrap(block, setIndex, resetIndex)))),
                EquivalenceKey),
            diagnostic);
    }

    private static BlockSyntax Wrap(BlockSyntax block, int setIndex, int resetIndex)
    {
        var statements = block.Statements;
        var guarded = statements.Skip(setIndex + 1).Take(resetIndex - setIndex - 1);
        var reset = statements[resetIndex].WithoutLeadingTrivia();
        var tryStatement = SyntaxFactory.TryStatement(
                SyntaxFactory.Block(guarded),
                default,
                SyntaxFactory.FinallyClause(SyntaxFactory.Block(reset)))
            .WithLeadingTrivia(statements[setIndex + 1].GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var rewritten = statements.Take(setIndex + 1)
            .Concat(new StatementSyntax[] { tryStatement })
            .Concat(statements.Skip(resetIndex + 1));
        return block.WithStatements(SyntaxFactory.List(rewritten));
    }

    private static bool DeclaredLocalsAreUsedAfter(BlockSyntax block, int firstGuarded, int resetIndex)
    {
        var statements = block.Statements;
        var declared = new HashSet<string>(StringComparer.Ordinal);
        for (var index = firstGuarded; index < resetIndex; index++)
        {
            foreach (var variable in statements[index].DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>())
            {
                declared.Add(variable.Identifier.ValueText);
            }

            foreach (var designation in statements[index].DescendantNodesAndSelf().OfType<SingleVariableDesignationSyntax>())
            {
                declared.Add(designation.Identifier.ValueText);
            }

            if (statements[index] is LocalFunctionStatementSyntax localFunction)
            {
                declared.Add(localFunction.Identifier.ValueText);
            }
        }

        if (declared.Count == 0)
        {
            return false;
        }

        for (var index = resetIndex + 1; index < statements.Count; index++)
        {
            if (statements[index].DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(name => declared.Contains(name.Identifier.ValueText)))
            {
                return true;
            }
        }

        return false;
    }
}
