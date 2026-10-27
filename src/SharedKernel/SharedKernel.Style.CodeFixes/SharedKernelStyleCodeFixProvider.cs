using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using PandaBear.Style.Analyzers;

namespace PandaBear.Style.CodeFixes;

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
            StyleDiagnosticIds.GenericTypeNameSuffix,
            StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
            StyleDiagnosticIds.DomainEventSuffix,
            StyleDiagnosticIds.SuccessOnlyResultMethod,
            StyleDiagnosticIds.DirectCurrentTime,
            StyleDiagnosticIds.CaseInsensitiveJsonContext,
            StyleDiagnosticIds.FileNameMismatch
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
            if (diagnostic.Id == StyleDiagnosticIds.DirectCurrentTime)
            {
                await UseTimeProviderCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

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

            if (diagnostic.Id == StyleDiagnosticIds.GenericTypeNameSuffix)
            {
                await RenameGenericTypeCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.BroadOperationCanceledExceptionFilter)
            {
                await UseShouldHandleAsFailureCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.DomainEventSuffix)
            {
                await RenameDomainEventTypeCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.CaseInsensitiveJsonContext)
            {
                await CaseInsensitiveJsonContextCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.FileNameMismatch)
            {
                await RenameFileToTypeNameCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }

            if (diagnostic.Id == StyleDiagnosticIds.SuccessOnlyResultMethod)
            {
                await SuccessOnlyResultMethodCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }
        }
    }
}
