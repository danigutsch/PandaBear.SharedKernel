using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Testing.Roslyn;

/// <summary>
/// An in-memory Roslyn workspace with a C# project and a main document <c>Test0.cs</c>, for running analyzers and code fix
/// providers over test source and inspecting what they report and change.
/// </summary>
public sealed class CodeFixWorkspace
{
    private const string MainDocumentName = "Test0.cs";
    private readonly ProjectId _projectId;
    private readonly DocumentId _documentId;
    private readonly string _defaultUsings;

    private CodeFixWorkspace(AdhocWorkspace workspace, ProjectId projectId, DocumentId documentId, string defaultUsings)
    {
        Workspace = workspace;
        _projectId = projectId;
        _documentId = documentId;
        _defaultUsings = defaultUsings;
    }

    /// <summary>Gets the underlying ad hoc workspace.</summary>
    public AdhocWorkspace Workspace { get; }

    /// <summary>Gets the current state of the project that holds the main document.</summary>
    /// <exception cref="InvalidOperationException">A change applied to the workspace removed the project.</exception>
    public Project Project =>
        Workspace.CurrentSolution.GetProject(_projectId) ?? throw new InvalidOperationException("The test project is no longer in the workspace.");

    /// <summary>Gets the current state of the main document.</summary>
    /// <exception cref="InvalidOperationException">A change applied to the workspace removed the document.</exception>
    public Document Document =>
        Workspace.CurrentSolution.GetDocument(_documentId) ?? throw new InvalidOperationException("The test document is no longer in the workspace.");

    /// <summary>Creates a workspace whose main document is the default usings followed by <paramref name="source"/>.</summary>
    /// <param name="source">The C# source of the main document.</param>
    /// <param name="options">How to build the project, or <see langword="null"/> for the defaults.</param>
    /// <returns>The workspace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The test process has no trusted platform assemblies to reference.</exception>
    public static CodeFixWorkspace Create(string source, CodeFixWorkspaceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new CodeFixWorkspaceOptions();
        var workspace = new AdhocWorkspace();
        var projectId = AddProject(workspace, options.AssemblyName, options);
        var documentId = AddDocument(workspace, projectId, MainDocumentName, options.DefaultUsings + source, options.DocumentPath);
        return new CodeFixWorkspace(workspace, projectId, documentId, options.DefaultUsings);
    }

    /// <summary>
    /// Creates a workspace whose project holds a <c>GlobalUsings.cs</c> document and a main document, both exactly as given:
    /// neither gets the default usings, so the main document relies on the global ones.
    /// </summary>
    /// <param name="source">The C# source of the main document.</param>
    /// <param name="globalUsingsSource">The C# source of <c>GlobalUsings.cs</c>, which comes first in the project.</param>
    /// <param name="options">How to build the project, or <see langword="null"/> for the defaults; its default usings are not used.</param>
    /// <returns>The workspace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="globalUsingsSource"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The test process has no trusted platform assemblies to reference.</exception>
    public static CodeFixWorkspace CreateWithGlobalUsings(string source, string globalUsingsSource, CodeFixWorkspaceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(globalUsingsSource);
        options ??= new CodeFixWorkspaceOptions();
        var workspace = new AdhocWorkspace();
        var projectId = AddProject(workspace, options.AssemblyName, options);
        _ = AddDocument(workspace, projectId, "GlobalUsings.cs", globalUsingsSource, SiblingPath(options.DocumentPath, "GlobalUsings.cs"));
        var documentId = AddDocument(workspace, projectId, MainDocumentName, source, options.DocumentPath);
        return new CodeFixWorkspace(workspace, projectId, documentId, string.Empty);
    }

    /// <summary>
    /// Creates a workspace with two projects: a module project with the document <c>Module.cs</c>, and the main project, which
    /// references it, with the main document <c>Primary.cs</c>. Both documents start with the default usings.
    /// </summary>
    /// <param name="primarySource">The C# source of the main document.</param>
    /// <param name="moduleSource">The C# source of the module document.</param>
    /// <param name="moduleAssemblyName">The project and assembly name of the module.</param>
    /// <param name="options">How to build both projects, or <see langword="null"/> for the defaults; its assembly name names the main project.</param>
    /// <returns>The workspace, whose <see cref="Project"/> and <see cref="Document"/> are the main ones.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="primarySource"/> or <paramref name="moduleSource"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="moduleAssemblyName"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="InvalidOperationException">The test process has no trusted platform assemblies to reference.</exception>
    public static CodeFixWorkspace CreateWithProjectReference(
        string primarySource,
        string moduleSource,
        string moduleAssemblyName,
        CodeFixWorkspaceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(primarySource);
        ArgumentNullException.ThrowIfNull(moduleSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleAssemblyName);
        options ??= new CodeFixWorkspaceOptions();
        var workspace = new AdhocWorkspace();
        var moduleProjectId = AddProject(workspace, moduleAssemblyName, options);
        _ = AddDocument(workspace, moduleProjectId, "Module.cs", options.DefaultUsings + moduleSource, SiblingPath(options.DocumentPath, "Module.cs"));
        var primaryProjectId = AddProject(workspace, options.AssemblyName, options);
        var primaryDocumentId = AddDocument(workspace, primaryProjectId, "Primary.cs", options.DefaultUsings + primarySource, SiblingPath(options.DocumentPath, "Primary.cs"));
        _ = workspace.TryApplyChanges(workspace.CurrentSolution.AddProjectReference(primaryProjectId, new ProjectReference(moduleProjectId)));
        return new CodeFixWorkspace(workspace, primaryProjectId, primaryDocumentId, options.DefaultUsings);
    }

    /// <summary>Adds a document made of the default usings followed by <paramref name="source"/> to the main project.</summary>
    /// <param name="source">The C# source of the document.</param>
    /// <param name="name">The file name of the document, which lies beside the main document.</param>
    /// <returns>The identifier of the new document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty, or white space.</exception>
    public DocumentId AddDocument(string source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = Document.FilePath is { } mainPath ? SiblingPath(mainPath, name) : "/" + name;
        return AddDocument(Workspace, _projectId, name, _defaultUsings + source, path);
    }

    /// <summary>Creates a warning diagnostic whose location is the first occurrence of <paramref name="markerText"/> in the main document.</summary>
    /// <param name="diagnosticId">The identifier, title, and message of the diagnostic.</param>
    /// <param name="markerText">The text whose first occurrence becomes the location.</param>
    /// <param name="properties">The properties of the diagnostic, or <see langword="null"/> for none.</param>
    /// <returns>The diagnostic, for a code fix provider to fix.</returns>
    /// <exception cref="ArgumentException"><paramref name="diagnosticId"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="markerText"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The main document does not contain <paramref name="markerText"/>.</exception>
    public Task<Diagnostic> CreateDocumentDiagnostic(string diagnosticId, string markerText, ImmutableDictionary<string, string?>? properties = null) =>
        CreateDiagnostic(Document, diagnosticId, markerText, properties);

    /// <summary>Creates a warning diagnostic whose location is the first occurrence of <paramref name="markerText"/> in the named document of any project.</summary>
    /// <param name="diagnosticId">The identifier, title, and message of the diagnostic.</param>
    /// <param name="documentName">The file name of the document, which must be unique in the workspace.</param>
    /// <param name="markerText">The text whose first occurrence becomes the location.</param>
    /// <param name="properties">The properties of the diagnostic, or <see langword="null"/> for none.</param>
    /// <returns>The diagnostic, for a code fix provider to fix.</returns>
    /// <exception cref="ArgumentException"><paramref name="diagnosticId"/> or <paramref name="documentName"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="markerText"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No single document has that name, or it does not contain <paramref name="markerText"/>.</exception>
    public Task<Diagnostic> CreateDocumentDiagnostic(
        string diagnosticId,
        string documentName,
        string markerText,
        ImmutableDictionary<string, string?>? properties = null) =>
        CreateDiagnostic(FindDocument(documentName), diagnosticId, markerText, properties);

    /// <summary>Asks <paramref name="provider"/> to register its fixes for <paramref name="diagnostic"/> and returns them.</summary>
    /// <param name="provider">The code fix provider under test.</param>
    /// <param name="diagnostic">The diagnostic to fix; its document is the one it lies in, or the main document when it has no source.</param>
    /// <returns>The code actions the provider registered, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> or <paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<CodeAction>> GetCodeActions(CodeFixProvider provider, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(diagnostic);
        var document = diagnostic.Location.SourceTree is { } tree
            ? Workspace.CurrentSolution.GetDocument(tree) ?? Document
            : Document;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        return actions;
    }

    /// <summary>Compiles the main project and runs <paramref name="analyzer"/> over it.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="enabledDiagnosticId">The identifier of a rule that is off by default to switch on as a warning, or <see langword="null"/>.</param>
    /// <returns>The diagnostics the analyzer reported.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="analyzer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The project could not be compiled.</exception>
    public async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnostics(DiagnosticAnalyzer analyzer, string? enabledDiagnosticId = null)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        var compilation = await GetCompilation().ConfigureAwait(false);
        if (enabledDiagnosticId is not null)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItem(enabledDiagnosticId, ReportDiagnostic.Warn)));
        }

        return await AnalyzerRun.GetDiagnostics(compilation, analyzer).ConfigureAwait(false);
    }

    /// <summary>Compiles the main project and returns the compiler's own diagnostics.</summary>
    /// <returns>The compilation diagnostics.</returns>
    /// <exception cref="InvalidOperationException">The project could not be compiled.</exception>
    public async Task<IReadOnlyList<Diagnostic>> GetCompilationDiagnostics() =>
        (await GetCompilation().ConfigureAwait(false)).GetDiagnostics();

    /// <summary>Applies <paramref name="action"/>, which must change the solution exactly once, to the workspace.</summary>
    /// <param name="action">The code action to apply.</param>
    /// <returns>A task that completes when every operation of the action has been applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The action has no solution change or more than one.</exception>
    public async Task ApplyCodeAction(CodeAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var changes = operations.OfType<ApplyChangesOperation>().Count();
        if (changes != 1)
        {
            throw new InvalidOperationException($"The code action '{action.Title}' has {changes} solution changes; exactly one was expected.");
        }

        foreach (var operation in operations)
        {
            operation.Apply(Workspace, CancellationToken.None);
        }
    }

    /// <summary>Returns the current text of the main document, default usings included.</summary>
    /// <returns>The text.</returns>
    public async Task<string> GetDocumentText() =>
        (await Document.GetTextAsync().ConfigureAwait(false)).ToString();

    /// <summary>Returns the current text of the document with the given identifier.</summary>
    /// <param name="documentId">The document to read.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documentId"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The workspace has no such document.</exception>
    public async Task<string> GetDocumentText(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        var document = Workspace.CurrentSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("The workspace has no document with that identifier.");
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>Returns the current text of the document of any project with the given file name, such as one a code fix added.</summary>
    /// <param name="documentName">The file name of the document, which must be unique in the workspace.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException"><paramref name="documentName"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="InvalidOperationException">No single document has that name.</exception>
    public async Task<string> GetDocumentText(string documentName) =>
        (await FindDocument(documentName).GetTextAsync().ConfigureAwait(false)).ToString();

    private async Task<Compilation> GetCompilation() =>
        await Project.GetCompilationAsync().ConfigureAwait(false)
        ?? throw new InvalidOperationException("The test project could not be compiled.");

    private Document FindDocument(string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        var matches = Workspace.CurrentSolution.Projects
            .SelectMany(static project => project.Documents)
            .Where(document => string.Equals(document.Name, documentName, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException($"The workspace has {matches.Length} documents named '{documentName}'; exactly one was expected.");
    }

    private static async Task<Diagnostic> CreateDiagnostic(
        Document document,
        string diagnosticId,
        string markerText,
        ImmutableDictionary<string, string?>? properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentNullException.ThrowIfNull(markerText);
        var text = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
        var start = text.IndexOf(markerText, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Could not find marker text '{markerText}' in document '{document.Name}'.");
        }

        var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The document '{document.Name}' has no syntax tree.");
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title: diagnosticId,
            messageFormat: diagnosticId,
            category: "Test",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        return Diagnostic.Create(descriptor, Location.Create(syntaxTree, new TextSpan(start, markerText.Length)), properties);
    }

    private static ProjectId AddProject(AdhocWorkspace workspace, string assemblyName, CodeFixWorkspaceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        var directory = Path.GetDirectoryName(options.DocumentPath) is { Length: > 0 } documentDirectory ? documentDirectory : Path.GetTempPath();
        var projectId = ProjectId.CreateNewId(assemblyName);
        workspace.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            name: assemblyName,
            assemblyName: assemblyName,
            language: LanguageNames.CSharp,
            filePath: Path.Combine(directory, assemblyName + ".csproj"),
            outputFilePath: Path.Combine(directory, assemblyName + ".dll"),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: options.NullableContextOptions),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview, options.DocumentationMode),
            metadataReferences: [.. AnalyzerTestHarness.GetTrustedPlatformReferences(), .. options.AdditionalReferences]));
        return projectId;
    }

    private static DocumentId AddDocument(AdhocWorkspace workspace, ProjectId projectId, string name, string text, string path)
    {
        var documentId = DocumentId.CreateNewId(projectId, name);
        workspace.AddDocument(DocumentInfo.Create(
            documentId,
            name,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())),
            filePath: path));
        return documentId;
    }

    private static string SiblingPath(string path, string name) =>
        Path.GetDirectoryName(path) is { Length: > 0 } directory ? Path.Combine(directory, name) : name;
}
