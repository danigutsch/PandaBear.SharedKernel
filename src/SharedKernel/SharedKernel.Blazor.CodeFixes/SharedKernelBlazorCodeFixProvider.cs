using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using PandaBear.Blazor.Analyzers;

namespace PandaBear.Blazor.CodeFixes;

/// <summary>
/// Provides the safe code fixes for the SharedKernel Blazor component analyzers.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelBlazorCodeFixProvider))]
public sealed class SharedKernelBlazorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [
            BlazorDiagnosticIds.UnguardedBusyFlag,
            BlazorDiagnosticIds.LongLivedScopeService
        ];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case BlazorDiagnosticIds.UnguardedBusyFlag:
                    await ResetBusyFlagInFinallyCodeFix.Register(context, diagnostic).ConfigureAwait(false);
                    break;
                case BlazorDiagnosticIds.LongLivedScopeService:
                    await UseScopedSenderCodeFix.Register(context, diagnostic).ConfigureAwait(false);
                    break;
            }
        }
    }
}
