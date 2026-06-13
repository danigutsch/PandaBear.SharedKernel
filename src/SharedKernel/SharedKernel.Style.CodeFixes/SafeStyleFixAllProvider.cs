using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using SharedKernel.Style.Analyzers;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Restricts Fix All support to the safe bulk-rename style diagnostic set.
/// </summary>
internal sealed class SafeStyleFixAllProvider : FixAllProvider
{
    private static readonly FixAllProvider BatchFixer = WellKnownFixAllProviders.BatchFixer;

    private static readonly ImmutableArray<string> SupportedDiagnosticIds =
        [StyleDiagnosticIds.AsyncSuffix, StyleDiagnosticIds.CancellationTokenParameterName];

    /// <summary>
    /// Gets the singleton fix-all provider instance for style code fixes.
    /// </summary>
    public static SafeStyleFixAllProvider Instance { get; } = new();

    public override IEnumerable<string> GetSupportedFixAllDiagnosticIds(CodeFixProvider originalCodeFixProvider)
    {
        if (originalCodeFixProvider is null)
        {
            throw new ArgumentNullException(nameof(originalCodeFixProvider));
        }

        return SupportedDiagnosticIds;
    }

    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes()
    {
        return BatchFixer.GetSupportedFixAllScopes();
    }

    public override Task<Microsoft.CodeAnalysis.CodeActions.CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        return BatchFixer.GetFixAsync(fixAllContext);
    }
}
