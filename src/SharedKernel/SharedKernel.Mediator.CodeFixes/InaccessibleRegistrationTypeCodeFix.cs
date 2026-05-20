using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Repairs inaccessible registration types by making them public or granting friend access.
/// </summary>
internal static class InaccessibleRegistrationTypeCodeFix
{
    private const string DiagnosticPropertyPrimaryAssemblyName = "PrimaryAssemblyName";

    /// <summary>
    /// Registers safe fixes for inaccessible registration diagnostics.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The inaccessible registration diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var typeDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<TypeDeclarationSyntax>();

        if (typeDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Make type public",
                createChangedDocument: cancellationToken => MakeTypePublicAsync(context.Document, diagnostic, cancellationToken),
                equivalenceKey: "MakeMediatorRegistrationTypePublic"),
            diagnostic);

        if (diagnostic.Properties.TryGetValue(DiagnosticPropertyPrimaryAssemblyName, out var primaryAssemblyNameValue)
            && !string.IsNullOrWhiteSpace(primaryAssemblyNameValue))
        {
            var primaryAssemblyName = primaryAssemblyNameValue!;
            if (string.Equals(context.Document.Project.AssemblyName, primaryAssemblyName, StringComparison.Ordinal)
                || await HasInternalsVisibleToAsync(context.Document.Project, primaryAssemblyName, context.CancellationToken).ConfigureAwait(false))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add InternalsVisibleTo(\"{primaryAssemblyName}\")",
                    createChangedSolution: cancellationToken => AddInternalsVisibleToAsync(context.Document, primaryAssemblyName, cancellationToken),
                    equivalenceKey: $"AddMediatorInternalsVisibleTo:{primaryAssemblyName}"),
                diagnostic);
        }
    }

    private static async Task<Document> MakeTypePublicAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var targetDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<TypeDeclarationSyntax>();

        if (targetDeclaration is null)
        {
            return document;
        }

        var declarations = targetDeclaration.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().ToArray();
        var annotations = declarations.ToDictionary(
            static declaration => declaration,
            static _ => new SyntaxAnnotation());
        var updatedRoot = root.ReplaceNodes(
            declarations,
            (original, _) => original.WithAdditionalAnnotations(annotations[original]));
        for (var index = declarations.Length - 1; index >= 0; index--)
        {
            var currentDeclaration = updatedRoot.GetAnnotatedNodes(annotations[declarations[index]])
                .OfType<TypeDeclarationSyntax>()
                .SingleOrDefault();
            if (currentDeclaration is null)
            {
                continue;
            }

            updatedRoot = updatedRoot.ReplaceNode(currentDeclaration, MakeDeclarationPublic(currentDeclaration));
        }

        return document.WithSyntaxRoot(updatedRoot);
    }

    private static TypeDeclarationSyntax MakeDeclarationPublic(TypeDeclarationSyntax declaration)
    {
        var filteredModifiers = declaration.Modifiers
            .Where(
                static token => !token.IsKind(SyntaxKind.PublicKeyword)
                                && !token.IsKind(SyntaxKind.InternalKeyword)
                                && !token.IsKind(SyntaxKind.PrivateKeyword)
                                && !token.IsKind(SyntaxKind.ProtectedKeyword)
                                && !token.IsKind(SyntaxKind.FileKeyword))
            .ToArray();
        var publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = SyntaxFactory.TokenList(
            Enumerable.Repeat(publicToken, 1).Concat(filteredModifiers));

        return declaration
            .WithModifiers(modifiers)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static async Task<bool> HasInternalsVisibleToAsync(
        Project project,
        string primaryAssemblyName,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return false;
        }

        var attributeSymbol = compilation.GetTypeByMetadataName(MetadataNames.InternalsVisibleToAttribute);
        if (attributeSymbol is null)
        {
            return false;
        }

        return compilation.Assembly.GetAttributes().Any(
            attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol)
                         && attribute.ConstructorArguments.Length == 1
                         && attribute.ConstructorArguments[0].Value is string friendAssemblyName
                         && string.Equals(friendAssemblyName, primaryAssemblyName, StringComparison.Ordinal));
    }

    private static Task<Solution> AddInternalsVisibleToAsync(
        Document document,
        string primaryAssemblyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sanitizedAssemblyName = string.Concat(
            primaryAssemblyName.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var fileName = $"MediatorInternalsVisibleTo.{sanitizedAssemblyName}.cs";
        var source = $$"""
            [assembly: global::System.Runtime.CompilerServices.InternalsVisibleTo("{{primaryAssemblyName}}")]
            """;
        var filePath = document.FilePath is null
            ? fileName
            : Path.Combine(Path.GetDirectoryName(document.FilePath) ?? string.Empty, fileName);
        var updatedProject = document.Project.AddDocument(fileName, source, folders: document.Folders, filePath: filePath).Project;

        return Task.FromResult(updatedProject.Solution);
    }
}
