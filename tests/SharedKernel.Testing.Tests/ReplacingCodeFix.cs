using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Testing.Tests;

/// <summary>Fixes the diagnostic <see cref="Id"/> by replacing the text at its location with <c>Fixed</c>.</summary>
internal sealed class ReplacingCodeFix : CodeFixProvider
{
    /// <summary>The identifier of the diagnostic this provider fixes.</summary>
    public const string Id = "TEST002";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [Id];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var span = context.Span;
        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with Fixed",
                async cancellationToken =>
                {
                    var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    return document.WithText(text.WithChanges(new TextChange(span, "Fixed")));
                },
                equivalenceKey: Id),
            context.Diagnostics);
        return Task.CompletedTask;
    }
}
