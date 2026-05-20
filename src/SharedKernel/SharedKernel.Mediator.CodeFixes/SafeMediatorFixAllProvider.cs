using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Restricts Fix All support to code fixes that are safe to apply in bulk.
/// </summary>
internal sealed class SafeMediatorFixAllProvider : FixAllProvider
{
    private const string MissingArgumentDiagnosticId = "CS7036";
    private const string InvalidRequestArgumentDiagnosticId = "CS1503";

    private static readonly FixAllProvider BatchFixer = WellKnownFixAllProviders.BatchFixer;

    private static readonly ImmutableArray<string> SupportedDiagnosticIds =
    [
        MissingArgumentDiagnosticId,
        InvalidRequestArgumentDiagnosticId,
        MediatorDiagnosticIds.MissingHandler,
        MediatorDiagnosticIds.MissingCancellationToken,
        MediatorDiagnosticIds.MissingEnumeratorCancellation,
        MediatorDiagnosticIds.MissingModuleMarker
    ];

    public static SafeMediatorFixAllProvider Instance { get; } = new();

    /// <inheritdoc />
    public override IEnumerable<string> GetSupportedFixAllDiagnosticIds(CodeFixProvider originalCodeFixProvider)
    {
        if (originalCodeFixProvider is null)
        {
            throw new ArgumentNullException(nameof(originalCodeFixProvider));
        }

        return SupportedDiagnosticIds;
    }

    /// <inheritdoc />
    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes()
    {
        return BatchFixer.GetSupportedFixAllScopes();
    }

    /// <inheritdoc />
    public override Task<Microsoft.CodeAnalysis.CodeActions.CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        return BatchFixer.GetFixAsync(fixAllContext);
    }
}
