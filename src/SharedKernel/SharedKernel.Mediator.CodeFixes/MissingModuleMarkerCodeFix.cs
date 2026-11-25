using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Adds the assembly-level mediator module marker required for cross-assembly discovery.
/// </summary>
internal static class MissingModuleMarkerCodeFix
{
    /// <summary>
    /// Registers the missing-module-marker fix when the current project is unmarked.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The missing module marker diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        if (await HasMediatorModuleAttributeAsync(context.Document.Project, context.CancellationToken).ConfigureAwait(false))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add [assembly: MediatorModule]",
                createChangedSolution: cancellationToken => AddMediatorModuleAttributeAsync(context.Document, cancellationToken),
                equivalenceKey: "AddMediatorModuleAssemblyAttribute"),
            diagnostic);
    }

    private static async Task<bool> HasMediatorModuleAttributeAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return false;
        }

        var attributeSymbol = compilation.GetTypeByMetadataName(MetadataNames.MediatorModuleAttribute);
        if (attributeSymbol is null)
        {
            return false;
        }

        return compilation.Assembly.GetAttributes().Any(
            attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));
    }

    private static Task<Solution> AddMediatorModuleAttributeAsync(Document document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string fileName = "MediatorModuleAssemblyInfo.cs";
        const string source = """
            [assembly: global::SharedKernel.Mediator.MediatorModuleAttribute]
            """;
        var filePath = document.FilePath is null
            ? fileName
            : Path.Combine(Path.GetDirectoryName(document.FilePath) ?? string.Empty, fileName);
        var updatedProject = document.Project.AddDocument(fileName, source, folders: document.Folders, filePath: filePath).Project;

        return Task.FromResult(updatedProject.Solution);
    }
}
