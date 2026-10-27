using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.CodeFixes.Testing;

/// <summary>In-memory Roslyn project holding one C# test document, used to run analyzers and code fix providers and inspect the results.</summary>
public sealed class CodeFixTestWorkspace
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    private CodeFixTestWorkspace(AdhocWorkspace workspace, ProjectId projectId, DocumentId documentId)
    {
        Workspace = workspace;
        ProjectId = projectId;
        DocumentId = documentId;
    }

    /// <summary>Gets the underlying ad hoc Roslyn workspace.</summary>
    public AdhocWorkspace Workspace
    {
        get;
    }

    private ProjectId ProjectId
    {
        get;
    }

    private DocumentId DocumentId
    {
        get;
    }

    private Document Document
    {
        get
        {
            var document = Workspace.CurrentSolution.GetDocument(DocumentId);
            return document ?? throw new InvalidOperationException("Test document could not be found.");
        }
    }

    /// <summary>Creates a workspace with one project referencing the trusted platform assemblies and one document <c>Test0.cs</c> made of common <c>using</c> directives followed by the given source.</summary>
    /// <param name="source">The C# source appended after the default usings.</param>
    /// <param name="assemblyName">The project and assembly name.</param>
    /// <returns>The new workspace.</returns>
    public static CodeFixTestWorkspace Create(string source, string assemblyName = "SharedKernel.Style.CodeFixes.Tests.Dynamic")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

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

    /// <summary>Adds a further document, prefixed with the default usings, to the project.</summary>
    /// <param name="source">The C# source appended after the default usings.</param>
    /// <param name="name">The document file name.</param>
    /// <returns>The identifier of the new document.</returns>
    public DocumentId AddDocument(string source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var documentId = DocumentId.CreateNewId(ProjectId, name);
        Workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                name,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(DefaultUsings + source), VersionStamp.Create())),
                filePath: $"/{name}"));
        return documentId;
    }

    /// <summary>Creates a warning diagnostic with the given id whose location spans the first occurrence of the marker text in the main document.</summary>
    /// <param name="diagnosticId">The diagnostic identifier.</param>
    /// <param name="markerText">The text to locate in the document.</param>
    /// <returns>The synthetic diagnostic.</returns>
    public async Task<Diagnostic> CreateDocumentDiagnostic(string diagnosticId, string markerText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentNullException.ThrowIfNull(markerText);

        var text = await Document.GetTextAsync().ConfigureAwait(false);
        var source = text.ToString();
        var start = source.IndexOf(markerText, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Could not find marker text '{markerText}'.");
        }

        var syntaxTree = await Document.GetSyntaxTreeAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Test document syntax tree could not be loaded.");
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title: diagnosticId,
            messageFormat: diagnosticId,
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.Create(syntaxTree, new TextSpan(start, markerText.Length)));
    }

    /// <summary>Asks the provider to register its code fixes for the diagnostic in the main document and collects the registered actions.</summary>
    /// <param name="provider">The code fix provider under test.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The code actions the provider registered.</returns>
    public async Task<IReadOnlyList<CodeAction>> GetCodeActions(CodeFixProvider provider, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(diagnostic);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            Document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        return actions;
    }

    /// <summary>Compiles the project and runs the analyzer over it.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <returns>The diagnostics the analyzer reported.</returns>
    public async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnostics(DiagnosticAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);

        var project = Workspace.CurrentSolution.GetProject(ProjectId) ?? throw new InvalidOperationException("Test project could not be found.");
        var compilation = await project.GetCompilationAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Test compilation could not be created.");
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>Applies the single change operation of the code action to the workspace solution.</summary>
    /// <param name="action">The code action to apply.</param>
    /// <returns>A task that completes when the changes have been applied.</returns>
    public async Task ApplyCodeAction(CodeAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        Workspace.TryApplyChanges(applyOperation.ChangedSolution);
    }

    /// <summary>Gets the current text of the main document, including the default usings.</summary>
    /// <returns>The document text.</returns>
    public async Task<string> GetDocumentText()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>Gets the current text of the document with the given identifier.</summary>
    /// <param name="documentId">The document to read.</param>
    /// <returns>The document text.</returns>
    public async Task<string> GetDocumentText(DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);

        var document = Workspace.CurrentSolution.GetDocument(documentId) ?? throw new InvalidOperationException("Test document could not be found.");
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>Compiles the project and returns the compiler's own diagnostics.</summary>
    /// <returns>The compilation diagnostics.</returns>
    public async Task<IReadOnlyList<Diagnostic>> GetCompilationDiagnostics()
    {
        var project = Workspace.CurrentSolution.GetProject(ProjectId) ?? throw new InvalidOperationException("Test project could not be found.");
        var compilation = await project.GetCompilationAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Test compilation could not be created.");
        return compilation.GetDiagnostics();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not available.");
        }

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }
}
