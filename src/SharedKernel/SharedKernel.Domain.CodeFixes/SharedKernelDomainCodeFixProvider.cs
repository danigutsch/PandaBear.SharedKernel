using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using PandaBear.Domain.Analyzers;

namespace PandaBear.Domain.CodeFixes;

/// <summary>
/// Provides safe SharedKernel domain-model encapsulation code fixes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelDomainCodeFixProvider))]
public sealed class SharedKernelDomainCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [
            DomainDiagnosticIds.NonPrivateSetter
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
            if (diagnostic.Id == DomainDiagnosticIds.NonPrivateSetter)
            {
                await MakeSetterPrivateCodeFix.Register(context, diagnostic).ConfigureAwait(false);
            }
        }
    }
}
