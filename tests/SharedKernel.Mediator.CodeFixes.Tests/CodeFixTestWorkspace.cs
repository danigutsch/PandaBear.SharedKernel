using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes.Tests;

/// <summary>
/// Creates and mutates an in-memory workspace for mediator code-fix tests.
/// </summary>
internal sealed class CodeFixTestWorkspace
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    private CodeFixTestWorkspace(AdhocWorkspace workspace, ProjectId projectId, DocumentId documentId)
    {
        Workspace = workspace;
        ProjectId = projectId;
        DocumentId = documentId;
    }

    /// <summary>
    /// Gets the mutable Roslyn workspace.
    /// </summary>
    public AdhocWorkspace Workspace { get; }

    private ProjectId ProjectId { get; }

    private DocumentId DocumentId { get; }

    private Project Project => Workspace.CurrentSolution.GetProject(ProjectId)!;

    private Document Document => Workspace.CurrentSolution.GetDocument(DocumentId)!;

    /// <summary>
    /// Creates a new workspace containing a single C# document.
    /// </summary>
    /// <param name="source">The source to place in the primary document.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The initialized test workspace.</returns>
    public static CodeFixTestWorkspace Create(
        string source,
        string assemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Dynamic")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId(assemblyName);
        var versionStamp = VersionStamp.Create();
        var documentId = DocumentId.CreateNewId(projectId, "Test0.cs");
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            name: assemblyName,
            assemblyName: assemblyName,
            language: LanguageNames.CSharp,
            filePath: $"/{assemblyName}.csproj",
            outputFilePath: $"/{assemblyName}.dll",
            compilationOptions: compilationOptions,
            parseOptions: parseOptions,
            metadataReferences: GetMetadataReferences());

        workspace.AddProject(projectInfo);
        workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                "Test0.cs",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(DefaultUsings + source), versionStamp)),
                filePath: "/Test0.cs"));

        return new CodeFixTestWorkspace(workspace, projectId, documentId);
    }

    /// <summary>
    /// Creates a two-project workspace with the primary project referencing a module project.
    /// </summary>
    /// <param name="primarySource">The source placed in the primary project.</param>
    /// <param name="moduleSource">The source placed in the referenced module project.</param>
    /// <param name="primaryAssemblyName">The dynamic primary assembly name.</param>
    /// <param name="moduleAssemblyName">The dynamic module assembly name.</param>
    /// <returns>The initialized multi-project workspace.</returns>
    public static CodeFixTestWorkspace CreateWithProjectReference(
        string primarySource,
        string moduleSource,
        string primaryAssemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Primary",
        string moduleAssemblyName = "SharedKernel.Mediator.CodeFixes.Tests.Module")
    {
        var workspace = new AdhocWorkspace();
        var versionStamp = VersionStamp.Create();
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var metadataReferences = GetMetadataReferences().ToList();

        var moduleProjectId = ProjectId.CreateNewId(moduleAssemblyName);
        var moduleDocumentId = DocumentId.CreateNewId(moduleProjectId, "Module.cs");
        var primaryProjectId = ProjectId.CreateNewId(primaryAssemblyName);
        var primaryDocumentId = DocumentId.CreateNewId(primaryProjectId, "Primary.cs");

        var solution = workspace.CurrentSolution
            .AddProject(
                ProjectInfo.Create(
                    moduleProjectId,
                    versionStamp,
                    name: moduleAssemblyName,
                    assemblyName: moduleAssemblyName,
                    language: LanguageNames.CSharp,
                    filePath: $"/{moduleAssemblyName}.csproj",
                    outputFilePath: $"/{moduleAssemblyName}.dll",
                    compilationOptions: compilationOptions,
                    parseOptions: parseOptions,
                    metadataReferences: metadataReferences))
            .AddDocument(
                DocumentInfo.Create(
                    moduleDocumentId,
                    "Module.cs",
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(DefaultUsings + moduleSource), versionStamp)),
                    filePath: "/Module.cs"))
            .AddProject(
                ProjectInfo.Create(
                    primaryProjectId,
                    versionStamp,
                    name: primaryAssemblyName,
                    assemblyName: primaryAssemblyName,
                    language: LanguageNames.CSharp,
                    filePath: $"/{primaryAssemblyName}.csproj",
                    outputFilePath: $"/{primaryAssemblyName}.dll",
                    compilationOptions: compilationOptions,
                    parseOptions: parseOptions,
                    metadataReferences: metadataReferences))
            .AddDocument(
                DocumentInfo.Create(
                    primaryDocumentId,
                    "Primary.cs",
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(DefaultUsings + primarySource), versionStamp)),
                    filePath: "/Primary.cs"))
            .AddProjectReference(primaryProjectId, new ProjectReference(moduleProjectId));
        workspace.TryApplyChanges(solution);

        return new CodeFixTestWorkspace(workspace, primaryProjectId, primaryDocumentId);
    }

    /// <summary>
    /// Gets all generator diagnostics for the current workspace state.
    /// </summary>
    /// <returns>The diagnostics emitted by the mediator source generator.</returns>
    public async Task<ImmutableArray<Diagnostic>> GetGeneratorDiagnosticsAsync()
    {
        var compilation = (CSharpCompilation?)await Project.GetCompilationAsync().ConfigureAwait(false);
        Assert.NotNull(compilation);

        var generator = new SharedKernelMediatorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult().Results.Single().Diagnostics;
    }

    /// <summary>
    /// Gets the first generator diagnostic with the requested identifier.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic identifier to locate.</param>
    /// <returns>The matching diagnostic.</returns>
    public async Task<Diagnostic> GetSingleGeneratorDiagnosticAsync(string diagnosticId)
    {
        var diagnostics = await GetGeneratorDiagnosticsAsync().ConfigureAwait(false);
        return Assert.Single(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    /// <summary>
    /// Creates a synthetic diagnostic on the named document for code-fix testing.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic identifier to create.</param>
    /// <param name="documentName">The document name hosting the diagnostic.</param>
    /// <param name="markerText">The text to locate for the diagnostic span.</param>
    /// <param name="properties">Optional diagnostic properties.</param>
    /// <returns>The created diagnostic.</returns>
    public async Task<Diagnostic> CreateDocumentDiagnosticAsync(
        string diagnosticId,
        string documentName,
        string markerText,
        ImmutableDictionary<string, string?>? properties = null)
    {
        var document = Assert.Single(
            Workspace.CurrentSolution.Projects.SelectMany(static project => project.Documents),
            candidate => string.Equals(candidate.Name, documentName, StringComparison.Ordinal));
        var text = await document.GetTextAsync().ConfigureAwait(false);
        var source = text.ToString();
        var start = source.IndexOf(markerText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find marker text '{markerText}' in document '{documentName}'.");
        var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
        Assert.NotNull(syntaxTree);
        var span = new TextSpan(start, markerText.Length);
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title: diagnosticId,
            messageFormat: diagnosticId,
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.Create(syntaxTree!, span), properties);
    }

    /// <summary>
    /// Gets the current primary document text.
    /// </summary>
    /// <returns>The latest source text from the primary document.</returns>
    public async Task<string> GetDocumentTextAsync()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>
    /// Gets a document text by file name across all projects in the workspace.
    /// </summary>
    /// <param name="documentName">The document name to locate.</param>
    /// <returns>The text from the requested document.</returns>
    public async Task<string> GetDocumentTextAsync(string documentName)
    {
        var document = Assert.Single(
            Workspace.CurrentSolution.Projects.SelectMany(static project => project.Documents),
            candidate => string.Equals(candidate.Name, documentName, StringComparison.Ordinal));
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>
    /// Gets a generated document text by file name.
    /// </summary>
    /// <param name="documentName">The document name to locate.</param>
    /// <returns>The text from the requested document.</returns>
    public async Task<string> GetAdditionalDocumentTextAsync(string documentName)
    {
        return await GetDocumentTextAsync(documentName).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a generated source by hint name from the current primary project compilation.
    /// </summary>
    /// <param name="hintName">The generated source hint name.</param>
    /// <returns>The generated source text.</returns>
    public async Task<string> GetGeneratedSourceAsync(string hintName)
    {
        var compilation = (CSharpCompilation?)await Project.GetCompilationAsync().ConfigureAwait(false);
        Assert.NotNull(compilation);

        var generator = new SharedKernelMediatorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        var generatedSource = Assert.Single(
            runResult.Results.Single().GeneratedSources,
            candidate => string.Equals(candidate.HintName, hintName, StringComparison.Ordinal));
        return generatedSource.SourceText.ToString();
    }

    /// <summary>
    /// Registers code actions for the provided diagnostic.
    /// </summary>
    /// <param name="provider">The provider under test.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The registered code actions.</returns>
    public async Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(CodeFixProvider provider, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var document = diagnostic.Location.SourceTree is null
            ? Document
            : Workspace.CurrentSolution.GetDocument(diagnostic.Location.SourceTree) ?? Document;
        var codeFixContext = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(codeFixContext).ConfigureAwait(false);
        return actions;
    }

    /// <summary>
    /// Applies the selected code action to the current workspace state.
    /// </summary>
    /// <param name="action">The action to apply.</param>
    public async Task ApplyCodeActionAsync(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);

        foreach (var operation in operations)
        {
            operation.Apply(Workspace, CancellationToken.None);
        }
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        ArgumentException.ThrowIfNullOrEmpty(trustedPlatformAssemblies);

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }

        yield return MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location);
    }
}
