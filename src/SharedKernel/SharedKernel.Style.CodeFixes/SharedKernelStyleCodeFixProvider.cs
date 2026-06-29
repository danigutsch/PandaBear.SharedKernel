using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using SharedKernel.Style.Analyzers;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Provides repository style code fixes, including placeholders that intentionally require manual completion.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelStyleCodeFixProvider))]
public sealed class SharedKernelStyleCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [
            StyleDiagnosticIds.AsyncSuffix,
            StyleDiagnosticIds.CancellationTokenParameterName,
            StyleDiagnosticIds.CancellationTokenDefaultValue,
            StyleDiagnosticIds.AspireImageTagAndDigest
        ];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return SafeStyleFixAllProvider.Instance;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == StyleDiagnosticIds.AsyncSuffix)
            {
                await RemoveAsyncSuffixCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.CancellationTokenParameterName)
            {
                await RenameCancellationTokenParameterCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.CancellationTokenDefaultValue)
            {
                await RemoveCancellationTokenDefaultValueCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.AspireImageTagAndDigest)
            {
                await AddAspireImagePinPlaceholderCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }
        }
    }
}
