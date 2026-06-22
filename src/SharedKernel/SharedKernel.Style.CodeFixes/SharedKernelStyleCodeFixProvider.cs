using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using SharedKernel.Style.Analyzers;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Provides safe code fixes for implemented SharedKernel style diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelStyleCodeFixProvider))]
public sealed class SharedKernelStyleCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [
            StyleDiagnosticIds.AsyncSuffix,
            StyleDiagnosticIds.CancellationTokenParameterName,
            StyleDiagnosticIds.CancellationTokenDefaultValue
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
        }
    }
}
