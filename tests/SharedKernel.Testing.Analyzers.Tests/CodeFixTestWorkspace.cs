using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Single-document ad hoc Roslyn workspace for running analyzers and code fix providers against test source.</summary>
internal sealed class CodeFixTestWorkspace
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        using Xunit;

        """;

    private CodeFixTestWorkspace(AdhocWorkspace workspace, DocumentId documentId)
    {
        Workspace = workspace;
        DocumentId = documentId;
    }

    /// <summary>Gets the underlying ad hoc workspace holding the project and document.</summary>
    public AdhocWorkspace Workspace
    {
        get;
    }

    private DocumentId DocumentId
    {
        get;
    }

    private Document Document => Workspace.CurrentSolution.GetDocument(DocumentId).ShouldBeOfType<Document>();

    /// <summary>Creates a workspace with one Test0.cs document, optionally prefixed with the default usings, referencing all trusted platform assemblies.</summary>
    /// <param name="source">C# source of the document.</param>
    /// <param name="assemblyName">Project and assembly name.</param>
    /// <param name="filePath">Path of the document, or null for a unique temporary path.</param>
    /// <param name="includeDefaultUsings">True to prepend the System, IO, threading, tasks and Xunit usings.</param>
    /// <param name="documentationMode">How XML documentation comments are parsed.</param>
    public static CodeFixTestWorkspace Create(
        string source,
        string assemblyName = "SharedKernel.Testing.CodeFixes.Tests.Dynamic",
        string? filePath = null,
        bool includeDefaultUsings = true,
        DocumentationMode documentationMode = DocumentationMode.Parse)
    {
        var documentFilePath = filePath
            ?? Path.Combine(Path.GetTempPath(), "PandaBear.Testing.CodeFixes.Tests", Guid.NewGuid().ToString("N"), "Test0.cs");
        var projectDirectory = Path.GetDirectoryName(documentFilePath) is { Length: > 0 } directory
            ? directory
            : Path.GetTempPath();
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId(assemblyName);
        var versionStamp = VersionStamp.Create();
        var documentId = DocumentId.CreateNewId(projectId, "Test0.cs");
        var documentSource = (includeDefaultUsings ? DefaultUsings : string.Empty) + source;
        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            name: assemblyName,
            assemblyName: assemblyName,
            language: LanguageNames.CSharp,
            filePath: Path.Combine(projectDirectory, assemblyName + ".csproj"),
            outputFilePath: Path.Combine(projectDirectory, assemblyName + ".dll"),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview, documentationMode),
            metadataReferences: GetMetadataReferences());

        workspace.AddProject(projectInfo);
        workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                "Test0.cs",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(documentSource), versionStamp)),
                filePath: documentFilePath));

        return new CodeFixTestWorkspace(workspace, documentId);
    }

    /// <summary>Creates a warning diagnostic with the given id located on the first occurrence of the marker text, without properties.</summary>
    /// <param name="diagnosticId">Id given to the diagnostic.</param>
    /// <param name="markerText">Text in the document whose first occurrence becomes the diagnostic location.</param>
    public Task<Diagnostic> CreateDocumentDiagnostic(string diagnosticId, string markerText)
    {
        return CreateDocumentDiagnostic(diagnosticId, markerText, properties: null);
    }

    /// <summary>Creates a warning diagnostic with the given id and properties located on the first occurrence of the marker text, failing the test if the text is absent.</summary>
    /// <param name="diagnosticId">Id given to the diagnostic.</param>
    /// <param name="markerText">Text in the document whose first occurrence becomes the diagnostic location.</param>
    /// <param name="properties">Properties attached to the diagnostic, or null.</param>
    public async Task<Diagnostic> CreateDocumentDiagnostic(
        string diagnosticId,
        string markerText,
        ImmutableDictionary<string, string?>? properties)
    {
        var text = await Document.GetTextAsync().ConfigureAwait(false);
        var source = text.ToString();
        var start = source.IndexOf(markerText, StringComparison.Ordinal);
        (start >= 0).ShouldBeTrue($"Could not find marker text '{markerText}'.");

        var syntaxTree = await Document.GetSyntaxTreeAsync().ConfigureAwait(false);
        var nonNullSyntaxTree = syntaxTree.ShouldBeAssignableTo<SyntaxTree>();

        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title: diagnosticId,
            messageFormat: diagnosticId,
            category: "Testing",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(
            descriptor,
            Location.Create(nonNullSyntaxTree, new TextSpan(start, markerText.Length)),
            properties,
            []);
    }

    /// <summary>Asks the provider to register its fixes for the diagnostic and returns the code actions it offered.</summary>
    /// <param name="provider">Code fix provider under test.</param>
    /// <param name="diagnostic">Diagnostic to fix.</param>
    public async Task<IReadOnlyList<CodeAction>> GetCodeActions(CodeFixProvider provider, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(Document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        return actions;
    }

    /// <summary>Runs the analyzer over the document's compilation and returns what it reports, first enabling a rule that is off by default when its id is given.</summary>
    /// <param name="analyzer">Analyzer under test.</param>
    /// <param name="enabledDiagnosticId">Id of a disabled-by-default rule to switch on, or null.</param>
    public async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnostics(DiagnosticAnalyzer analyzer, string? enabledDiagnosticId = null)
    {
        var project = Document.Project;
        var compilation = await project.GetCompilationAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Test compilation could not be created.");
        if (enabledDiagnosticId is not null)
        {
            // A rule that is disabled by default reports only when the compilation options switch it on.
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItem(enabledDiagnosticId, ReportDiagnostic.Warn)));
        }

        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>Applies the single change operation of the code action to the workspace.</summary>
    /// <param name="action">Code action whose changes are applied.</param>
    public async Task ApplyCodeAction(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().ShouldHaveSingleItem();
        Workspace.TryApplyChanges(applyOperation.ChangedSolution);
    }

    /// <summary>Returns the current text of the main test document.</summary>
    public async Task<string> GetDocumentText()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    /// <summary>Returns the current text of the project document with the given name.</summary>
    /// <param name="documentName">Name of the document to read.</param>
    public async Task<string> GetDocumentText(string documentName)
    {
        var document = Workspace.CurrentSolution.Projects.Single().Documents.SingleOrDefault(candidate => string.Equals(candidate.Name, documentName, StringComparison.Ordinal)).ShouldBeOfType<Document>();
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES").ShouldBeOfType<string>();
        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }

        yield return MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);
    }
}
