using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Style.CodeFixes.Tests;

internal static class SharedKernelStyleCodeFixProviderTestsHelpers
{
    public static bool InvokeIsRenamedMethodMatch(IMethodSymbol candidateSymbol, ISymbol originalSymbol, string updatedName)
    {
        var codeFixType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("SharedKernel.Style.CodeFixes.RemoveAsyncSuffixCodeFix");
        _ = TestAssert.NotNull(codeFixType);
        var method = codeFixType.GetMethod("IsRenamedMethodMatch", BindingFlags.NonPublic | BindingFlags.Static);
        _ = TestAssert.NotNull(method);
        return TestAssert.IsType<bool>(method.Invoke(null, [candidateSymbol, originalSymbol, updatedName]));
    }

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

        return TestAssert.IsType<Project>(workspace.CurrentSolution.GetProject(projectId));
    }

    public static async Task<Solution> OrganizeOverloads(
            Solution solution,
            DocumentId documentId,
            MethodDeclarationSyntax targetMethod,
            string updatedName,
            CancellationToken ct)
    {
        var organizerType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("SharedKernel.Style.CodeFixes.MethodOverloadGroupOrganizer");
        _ = TestAssert.NotNull(organizerType);
        var organizeMethod = organizerType.GetMethod("Organize", BindingFlags.Public | BindingFlags.Static);
        _ = TestAssert.NotNull(organizeMethod);
        var task = TestAssert.IsType<Task<Solution>>(organizeMethod.Invoke(null, [solution, documentId, targetMethod, updatedName, ct]));
        return await task.ConfigureAwait(false);
    }

    public static async Task<string> ReadDocumentText(Solution solution, DocumentId documentId)
    {
        var document = TestAssert.IsType<Document>(solution.GetDocument(documentId));
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        TestAssert.False(string.IsNullOrWhiteSpace(trustedPlatformAssemblies));
        var trustedAssemblyPaths = TestAssert.IsType<string>(trustedPlatformAssemblies);

        foreach (var path in trustedAssemblyPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }
}
