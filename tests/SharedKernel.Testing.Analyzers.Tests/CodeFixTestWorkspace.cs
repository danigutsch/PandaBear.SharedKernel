using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Testing.Analyzers.Tests;

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

    public AdhocWorkspace Workspace { get; }

    private DocumentId DocumentId { get; }

    private Document Document => Workspace.CurrentSolution.GetDocument(DocumentId).ShouldBeOfType<Document>();

    public static CodeFixTestWorkspace Create(
        string source,
        string assemblyName = "SharedKernel.Testing.CodeFixes.Tests.Dynamic",
        string filePath = "/Test0.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId(assemblyName);
        var versionStamp = VersionStamp.Create();
        var documentId = DocumentId.CreateNewId(projectId, "Test0.cs");
        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            name: assemblyName,
            assemblyName: assemblyName,
            language: LanguageNames.CSharp,
            filePath: $"/{assemblyName}.csproj",
            outputFilePath: $"/{assemblyName}.dll",
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            metadataReferences: GetMetadataReferences());

        workspace.AddProject(projectInfo);
        workspace.AddDocument(
            DocumentInfo.Create(
                documentId,
                "Test0.cs",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(DefaultUsings + source), versionStamp)),
                filePath: filePath));

        return new CodeFixTestWorkspace(workspace, documentId);
    }

    public Task<Diagnostic> CreateDocumentDiagnostic(string diagnosticId, string markerText)
    {
        return CreateDocumentDiagnostic(diagnosticId, markerText, properties: null);
    }

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

    public async Task<IReadOnlyList<CodeAction>> GetCodeActions(CodeFixProvider provider, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(Document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        return actions;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnostics(DiagnosticAnalyzer analyzer)
    {
        var project = Document.Project;
        var compilation = await project.GetCompilationAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("Test compilation could not be created.");
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    public async Task ApplyCodeAction(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().ShouldHaveSingleItem();
        Workspace.TryApplyChanges(applyOperation.ChangedSolution);
    }

    public async Task<string> GetDocumentText()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

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
