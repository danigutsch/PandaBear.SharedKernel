using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
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

    private Document Document => Assert.IsType<Document>(Workspace.CurrentSolution.GetDocument(DocumentId));

    public static CodeFixTestWorkspace Create(string source, string assemblyName = "SharedKernel.Testing.CodeFixes.Tests.Dynamic")
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
                filePath: "/Test0.cs"));

        return new CodeFixTestWorkspace(workspace, documentId);
    }

    public async Task<Diagnostic> CreateDocumentDiagnostic(string diagnosticId, string markerText)
    {
        var text = await Document.GetTextAsync().ConfigureAwait(false);
        var source = text.ToString();
        var start = source.IndexOf(markerText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find marker text '{markerText}'.");

        var syntaxTree = await Document.GetSyntaxTreeAsync().ConfigureAwait(false);
        var nonNullSyntaxTree = Assert.IsAssignableFrom<SyntaxTree>(syntaxTree);

        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            title: diagnosticId,
            messageFormat: diagnosticId,
            category: "Testing",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.Create(nonNullSyntaxTree, new TextSpan(start, markerText.Length)));
    }

    public async Task<IReadOnlyList<CodeAction>> GetCodeActions(CodeFixProvider provider, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(Document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        return actions;
    }

    public async Task ApplyCodeAction(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = Assert.Single(operations.OfType<ApplyChangesOperation>());
        Workspace.TryApplyChanges(applyOperation.ChangedSolution);
    }

    public async Task<string> GetDocumentText()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = Assert.IsType<string>(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }

        yield return MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location);
    }
}
