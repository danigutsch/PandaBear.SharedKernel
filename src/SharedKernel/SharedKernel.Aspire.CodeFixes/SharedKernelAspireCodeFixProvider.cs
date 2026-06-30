using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using SharedKernel.Aspire.Analyzers;

namespace SharedKernel.Aspire.CodeFixes;

/// <summary>
/// Provides SharedKernel Aspire code fixes, including placeholders that intentionally require manual completion.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelAspireCodeFixProvider))]
public sealed class SharedKernelAspireCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [
            AspireDiagnosticIds.ImageTagAndDigest
        ];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == AspireDiagnosticIds.ImageTagAndDigest)
            {
                await AddAspireImagePinPlaceholderCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }
        }
    }
}
