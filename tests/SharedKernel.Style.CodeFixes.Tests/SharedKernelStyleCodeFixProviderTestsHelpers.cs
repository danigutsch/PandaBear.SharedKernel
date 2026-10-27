using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.Style.CodeFixes.Tests;

/// <summary>Reflection and Roslyn workspace helpers for testing internal parts of the style code fix provider.</summary>
internal static class SharedKernelStyleCodeFixProviderTestsHelpers
{
    /// <summary>Calls the non-public IsRenamedMethodMatch of RemoveAsyncSuffixCodeFix by reflection and returns its result.</summary>
    /// <param name="candidateSymbol">Method symbol being tested as the renamed method.</param>
    /// <param name="originalSymbol">Symbol of the method before renaming.</param>
    /// <param name="updatedName">Name the method received after renaming.</param>
    public static bool InvokeIsRenamedMethodMatch(IMethodSymbol candidateSymbol, ISymbol originalSymbol, string updatedName)
    {
        var codeFixType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("PandaBear.Style.CodeFixes.RemoveAsyncSuffixCodeFix");
        _ = codeFixType.ShouldNotBeNull();
        var method = codeFixType.GetMethod("IsRenamedMethodMatch", BindingFlags.NonPublic | BindingFlags.Static);
        _ = method.ShouldNotBeNull();
        return method.Invoke(null, [candidateSymbol, originalSymbol, updatedName]).ShouldBeOfType<bool>();
    }

    /// <summary>Builds an ad hoc workspace project holding one Test0.cs document with the source and references to all trusted platform assemblies, returning the project and the document id.</summary>
    /// <param name="workspace">Workspace receiving the project.</param>
    /// <param name="source">Source text of the single document.</param>
    /// <param name="documentId">Receives the id of the created document.</param>
    /// <param name="assemblyName">Project and assembly name.</param>
    public static Project CreateProject(AdhocWorkspace workspace, string source, out DocumentId documentId, string assemblyName = "SharedKernel.Style.CodeFixes.Tests.Organizer")
    {
        var projectId = ProjectId.CreateNewId(assemblyName);
        var versionStamp = VersionStamp.Create();
        documentId = DocumentId.CreateNewId(projectId, "Test0.cs");
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
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), versionStamp)),
                filePath: "/Test0.cs"));

        return workspace.CurrentSolution.GetProject(projectId).ShouldBeOfType<Project>();
    }

    /// <summary>Calls the public Organize method of MethodOverloadGroupOrganizer by reflection and returns the resulting solution.</summary>
    /// <param name="solution">Solution containing the document.</param>
    /// <param name="documentId">Document holding the method.</param>
    /// <param name="targetMethod">Method declaration being renamed.</param>
    /// <param name="updatedName">New name of the method.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    public static async Task<Solution> OrganizeOverloads(
            Solution solution,
            DocumentId documentId,
            MethodDeclarationSyntax targetMethod,
            string updatedName,
            CancellationToken ct)
    {
        var organizerType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("PandaBear.Style.CodeFixes.MethodOverloadGroupOrganizer");
        _ = organizerType.ShouldNotBeNull();
        var organizeMethod = organizerType.GetMethod("Organize", BindingFlags.Public | BindingFlags.Static);
        _ = organizeMethod.ShouldNotBeNull();
        var task = organizeMethod.Invoke(null, [solution, documentId, targetMethod, updatedName, ct]).ShouldBeOfType<Task<Solution>>();
        return await task.ConfigureAwait(false);
    }

    /// <summary>Returns the current text of the given document in the solution.</summary>
    /// <param name="solution">Solution containing the document.</param>
    /// <param name="documentId">Id of the document to read.</param>
    public static async Task<string> ReadDocumentText(Solution solution, DocumentId documentId)
    {
        var document = solution.GetDocument(documentId).ShouldBeOfType<Document>();
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        string.IsNullOrWhiteSpace(trustedPlatformAssemblies).ShouldBeFalse();
        var trustedAssemblyPaths = trustedPlatformAssemblies.ShouldBeOfType<string>();

        foreach (var path in trustedAssemblyPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }
}
