using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PandaBear.Style.CodeFixes;

internal static class RenameFileToTypeNameCodeFix
{
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || string.IsNullOrEmpty(document.FilePath))
        {
            return;
        }

        var declaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<BaseTypeDeclarationSyntax>();
        if (declaration is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(document.FilePath) ?? string.Empty;
        var name = declaration.Identifier.ValueText;
        var candidates = new List<string> { name + ".cs" };
        if (declaration is TypeDeclarationSyntax { TypeParameterList: { } typeParameters })
        {
            candidates.Add(name + "{" + string.Join(",", typeParameters.Parameters.Select(static parameter => parameter.Identifier.ValueText)) + "}.cs");
        }

        var newFileName = candidates.FirstOrDefault(candidate => !document.Project.Documents.Any(other =>
            string.Equals(other.FilePath, Path.Combine(directory, candidate), StringComparison.OrdinalIgnoreCase)));
        if (newFileName is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename file to '{newFileName}'",
                createChangedSolution: cancellationToken => Task.FromResult(
                    document.Project.Solution
                        .WithDocumentName(document.Id, newFileName)
                        .WithDocumentFilePath(document.Id, Path.Combine(directory, newFileName))),
                equivalenceKey: $"RenameFileToTypeName:{newFileName}"),
            diagnostic);
    }
}
