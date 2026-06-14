using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace SharedKernel.Testing.CodeFixes;

/// <summary>
/// Restricts Fix All support until the testing code-fix family has a proven bulk-safe strategy.
/// </summary>
internal sealed class SafeTestingFixAllProvider : FixAllProvider
{
    private static readonly ImmutableArray<string> SupportedDiagnosticIds = [];
    private static readonly ImmutableArray<FixAllScope> SupportedScopes = [];

    /// <summary>
    /// Gets the singleton fix-all provider instance for testing code fixes.
    /// </summary>
    public static SafeTestingFixAllProvider Instance { get; } = new();

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
        return SupportedScopes;
    }

    public override Task<Microsoft.CodeAnalysis.CodeActions.CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        return Task.FromResult<Microsoft.CodeAnalysis.CodeActions.CodeAction?>(null);
    }
}
