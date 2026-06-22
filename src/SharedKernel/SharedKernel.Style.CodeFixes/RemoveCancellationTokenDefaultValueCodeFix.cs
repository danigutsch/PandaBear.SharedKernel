using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the default-value removal code fix for <c>SKSTYLE003</c> diagnostics.
/// </summary>
internal static class RemoveCancellationTokenDefaultValueCodeFix
{
    /// <summary>
    /// Registers a code action when the diagnostic targets a parameter with a default value.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var parameterSyntax = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ParameterSyntax>();
        if (parameterSyntax?.Default is null || HasPrecedingOptionalParameter(parameterSyntax))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove CancellationToken default value",
                createChangedDocument: _ => RemoveDefaultValue(context.Document, root, parameterSyntax),
                equivalenceKey: "RemoveCancellationTokenDefaultValue"),
            diagnostic);
    }

    private static bool HasPrecedingOptionalParameter(ParameterSyntax parameterSyntax)
    {
        if (parameterSyntax.Parent is not ParameterListSyntax parameterList)
        {
            return false;
        }

        foreach (var candidate in parameterList.Parameters)
        {
            if (candidate == parameterSyntax)
            {
                return false;
            }

            if (candidate.Default is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static Task<Document> RemoveDefaultValue(
        Document document,
        SyntaxNode root,
        ParameterSyntax parameterSyntax)
    {
        var updatedParameter = parameterSyntax
            .WithDefault(null)
            .WithIdentifier(parameterSyntax.Identifier.WithTrailingTrivia());
        var updatedRoot = root.ReplaceNode(parameterSyntax, updatedParameter);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }
}
