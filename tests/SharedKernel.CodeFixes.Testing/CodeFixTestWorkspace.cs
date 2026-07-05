using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.CodeFixes.Testing;

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

    public AdhocWorkspace Workspace { get; }

    private ProjectId ProjectId { get; }

    private DocumentId DocumentId { get; }

    private Document Document
    {
        get
        {
            var document = Workspace.CurrentSolution.GetDocument(DocumentId);
            return document ?? throw new InvalidOperationException("Test document could not be found.");
        }
    }

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

    public async Task ApplyCodeAction(CodeAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var operations = await action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        Workspace.TryApplyChanges(applyOperation.ChangedSolution);
    }

    public async Task<string> GetDocumentText()
    {
        return (await Document.GetTextAsync().ConfigureAwait(false)).ToString();
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
