using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace PandaBear.CodeFixes;

/// <summary>
/// Restricts Fix All to the diagnostics that are proven safe to fix in bulk, which it applies with the batch fixer; with none, it
/// offers no Fix All at all. The code-fix packages link this one source file and each passes its own list.
/// </summary>
/// <param name="diagnosticIds">The diagnostics that Fix All may fix.</param>
internal sealed class AllowListedFixAll(ImmutableArray<string> diagnosticIds) : FixAllProvider
{
    private static readonly FixAllProvider BatchFixer = WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override IEnumerable<string> GetSupportedFixAllDiagnosticIds(CodeFixProvider originalCodeFixProvider)
    {
        if (originalCodeFixProvider is null)
        {
            throw new ArgumentNullException(nameof(originalCodeFixProvider));
        }

        return diagnosticIds;
    }

    /// <inheritdoc />
    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes()
    {
        return diagnosticIds.IsEmpty ? [] : BatchFixer.GetSupportedFixAllScopes();
    }

    /// <inheritdoc />
    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        return diagnosticIds.IsEmpty ? Task.FromResult<CodeAction?>(null) : BatchFixer.GetFixAsync(fixAllContext);
    }
}
