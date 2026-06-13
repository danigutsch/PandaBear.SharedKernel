using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
namespace SharedKernel.Style.CodeFixes.Tests;

public sealed class SharedKernelStyleCodeFixProviderTests
{
    [Fact]
    public async Task Async_Suffix_Fix_Renames_Method_And_Reference()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("Load(CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("return Load(ct);", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadAsync", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Is_Not_Offered_When_Target_Name_Would_Conflict()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }

                public Task<string> Load(CancellationToken ct)
                {
                    return Task.FromResult("existing");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Is_Not_Offered_When_Base_Type_Already_Defines_Target_Name()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class BaseLoader
            {
                public virtual Task<string> Load(CancellationToken ct)
                {
                    return Task.FromResult("base");
                }
            }

            public sealed class TourLoader : BaseLoader
            {
                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Regroups_Overloads_When_Rename_Would_Split_Overload_Group()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route);
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("public async Task<string> Load(CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("public Task<string> Load(string route, CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("return Load(ct);", updatedText, StringComparison.Ordinal);
        var executeIndex = updatedText.IndexOf("public Task<string> Execute", StringComparison.Ordinal);
        var renamedOverloadIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var existingOverloadIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        Assert.True(executeIndex >= 0);
        Assert.True(renamedOverloadIndex >= 0);
        Assert.True(existingOverloadIndex >= 0);
        Assert.True(renamedOverloadIndex < existingOverloadIndex);
        Assert.True(existingOverloadIndex < executeIndex);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Regroups_Overloads_When_Earlier_References_Shift_Declaration_Position()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                private const string Prefix = nameof(LoadAsync) + nameof(LoadAsync);

                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route + Prefix);
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return Prefix;
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.DoesNotContain("nameof(LoadAsync)", updatedText, StringComparison.Ordinal);
        Assert.Contains("nameof(Load)", updatedText, StringComparison.Ordinal);
        var executeIndex = updatedText.IndexOf("public Task<string> Execute", StringComparison.Ordinal);
        var renamedOverloadIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var existingOverloadIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        Assert.True(executeIndex >= 0);
        Assert.True(renamedOverloadIndex >= 0);
        Assert.True(existingOverloadIndex >= 0);
        Assert.True(renamedOverloadIndex < existingOverloadIndex);
        Assert.True(existingOverloadIndex < executeIndex);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Orders_Overloads_By_Signature_Shape()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string route, CancellationToken ct)
                {
                    return Task.FromResult(route);
                }

                public Task<string> Load()
                {
                    return Task.FromResult("default");
                }

                public async Task<string> LoadAsync(CancellationToken ct)
                {
                    await Task.Yield();
                    return "VT-42";
                }

                public Task<string> Execute(CancellationToken ct)
                {
                    return LoadAsync(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync(CancellationToken ct)");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        var loadWithoutParametersIndex = updatedText.IndexOf("public Task<string> Load()", StringComparison.Ordinal);
        var loadWithCancellationTokenIndex = updatedText.IndexOf("public async Task<string> Load(CancellationToken ct)", StringComparison.Ordinal);
        var loadWithTwoParametersIndex = updatedText.IndexOf("public Task<string> Load(string route, CancellationToken ct)", StringComparison.Ordinal);
        Assert.Contains("Load(string route, CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("Load(CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("return Load(ct);", updatedText, StringComparison.Ordinal);
        Assert.True(loadWithoutParametersIndex >= 0);
        Assert.True(loadWithCancellationTokenIndex >= 0);
        Assert.True(loadWithTwoParametersIndex >= 0);
        Assert.True(loadWithoutParametersIndex < loadWithCancellationTokenIndex);
        Assert.True(loadWithCancellationTokenIndex < loadWithTwoParametersIndex);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Is_Not_Offered_For_Override_Methods()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public abstract class BaseReader : StringReader
            {
            }

            public sealed class DemoReader : BaseReader
            {
                public override Task<string?> ReadLineAsync()
                {
                    return Task.FromResult<string?>(string.Empty);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "ReadLineAsync");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task Async_Suffix_Fix_Is_Not_Offered_For_Interface_Implementations()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AsyncLifecycle : IAsyncDisposable
            {
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "DisposeAsync");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public async Task CancellationToken_Name_Fix_Renames_Parameter_And_References()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(\"VT-42\");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("Load(CancellationToken ct)", updatedText, StringComparison.Ordinal);
        Assert.Contains("ct.ThrowIfCancellationRequested();", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("cancellationToken", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationToken_Name_Fix_Is_Not_Offered_When_Ct_Already_Exists()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string ct, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(ct);
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelStyleCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.CancellationTokenParameterName, "CancellationToken cancellationToken");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Empty(codeActions);
    }

    [Fact]
    public void Fixable_Diagnostic_Ids_Match_Registered_Style_Fixes()
    {
        // Arrange
        CodeFixProvider provider = new SharedKernelStyleCodeFixProvider();

        // Act
        var diagnosticIds = provider.FixableDiagnosticIds.ToArray();

        // Assert
        Assert.Equal(
            [
                global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix,
                global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.CancellationTokenParameterName
            ],
            diagnosticIds);
    }

    [Fact]
    public void Fix_All_Is_Advertised_For_SKSTYLE001()
    {
        // Arrange
        var provider = new SharedKernelStyleCodeFixProvider();

        // Act
        var supportedDiagnosticIds = provider.GetFixAllProvider()
            .GetSupportedFixAllDiagnosticIds(provider)
            .ToArray();

        // Assert
        Assert.Equal(
            [
                global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix,
                global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.CancellationTokenParameterName
            ],
            supportedDiagnosticIds);
    }

    [Fact]
    public void Fix_All_Provider_Throws_When_Original_Provider_Is_Null()
    {
        // Arrange
        var fixAllProvider = new SharedKernelStyleCodeFixProvider().GetFixAllProvider();

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(() => fixAllProvider.GetSupportedFixAllDiagnosticIds(null!));
        Assert.Equal("originalCodeFixProvider", exception.ParamName);
    }

    [Fact]
    public void Fix_All_Provider_Exposes_The_Batch_Fixer_Scopes()
    {
        // Arrange
        var fixAllProvider = new SharedKernelStyleCodeFixProvider().GetFixAllProvider();

        // Act
        var supportedScopes = fixAllProvider.GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();
        var expectedScopes = WellKnownFixAllProviders.BatchFixer
            .GetSupportedFixAllScopes()
            .OrderBy(static scope => scope)
            .ToArray();

        // Assert
        Assert.Equal(expectedScopes, supportedScopes);
    }

    [Fact]
    public async Task Organizer_Returns_Original_Solution_When_Document_Is_Missing()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, source, out var documentId);
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        var targetMethod = Assert.Single(root.DescendantNodes().OfType<MethodDeclarationSyntax>());

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            DocumentId.CreateNewId(project.Id),
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(workspace.CurrentSolution, organizedSolution);
    }

    [Fact]
    public async Task Organizer_Returns_Original_Solution_When_Target_Method_Is_Not_Found_In_Document()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var sourceProject = CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var sourceDocumentId);
        var otherProject = CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class OtherLoader
            {
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var otherDocumentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.Other");
        var sourceDocument = Assert.IsType<Document>(sourceProject.GetDocument(sourceDocumentId));
        var sourceRoot = await sourceDocument.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(sourceRoot);
        var targetMethod = Assert.Single(sourceRoot.DescendantNodes().OfType<MethodDeclarationSyntax>());

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            otherDocumentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(workspace.CurrentSolution, organizedSolution);
        Assert.NotNull(otherProject.GetDocument(otherDocumentId));
    }

    [Fact]
    public async Task Organizer_Returns_Original_Solution_When_Target_Method_SyntaxTree_Differs_From_Document()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var documentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.SyntaxTreeMismatch");
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var detachedTree = CSharpSyntaxTree.ParseText(
            """
            namespace Demo;

            public sealed class DetachedLoader
            {
                public Task<string> LoadAsync(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken);
        var detachedRoot = await detachedTree.GetRootAsync(TestContext.Current.CancellationToken);
        var detachedMethod = Assert.Single(detachedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>());

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            detachedMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(workspace.CurrentSolution, organizedSolution);
        Assert.NotNull(document);
    }

    [Fact]
    public async Task Organizer_Orders_Overloads_With_Params_Modifier_Before_Non_Params()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(string[] values) => Task.FromResult(string.Empty);
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(params string[] values) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.Params");
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        var targetMethod = methods.Single(method => method.Identifier.ValueText == "Load" && method.ParameterList.Parameters[0].Modifiers.Count > 0);

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await ReadDocumentText(organizedSolution, documentId);

        // Assert
        var paramsIndex = updatedText.IndexOf("Load(params string[] values)", StringComparison.Ordinal);
        var regularIndex = updatedText.IndexOf("Load(string[] values)", StringComparison.Ordinal);
        Assert.True(paramsIndex >= 0);
        Assert.True(regularIndex >= 0);
        Assert.True(paramsIndex < regularIndex);
    }

    [Fact]
    public async Task Renamed_Method_Match_Returns_False_When_Original_Symbol_Is_Not_A_Method()
    {
        // Arrange
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(
            workspace,
            """
            namespace Demo;

            public sealed class TourLoader
            {
                public int Value { get; } = 42;

                public Task<string> Load(CancellationToken ct) => Task.FromResult(string.Empty);
            }
            """,
            out var documentId,
            assemblyName: "SharedKernel.Style.CodeFixes.Tests.SymbolMatch");
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(semanticModel);
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        var candidateMethod = Assert.Single(root.DescendantNodes().OfType<MethodDeclarationSyntax>());
        var candidateMethodSymbol = semanticModel.GetDeclaredSymbol(candidateMethod, TestContext.Current.CancellationToken);
        Assert.NotNull(candidateMethodSymbol);
        var propertyDeclaration = Assert.Single(root.DescendantNodes().OfType<PropertyDeclarationSyntax>());
        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, TestContext.Current.CancellationToken);
        Assert.NotNull(propertySymbol);

        // Act
        var isMatch = InvokeIsRenamedMethodMatch(
            candidateMethodSymbol,
            propertySymbol,
            updatedName: "Load");

        // Assert
        Assert.False(isMatch);
    }

    [Fact]
    public async Task Organizer_Orders_Overloads_By_Ref_Kind_When_Parameter_Count_And_Type_Match()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load(out int value) { value = 0; return Task.FromResult(string.Empty); }
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(ref int value) => Task.FromResult(string.Empty);
                public Task<string> Load(int value) => Task.FromResult(string.Empty);
                public Task<string> Load(in int value) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.RefKinds");
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        var targetMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await ReadDocumentText(organizedSolution, documentId);

        // Assert
        var valueIndex = updatedText.IndexOf("Load(int value)", StringComparison.Ordinal);
        var refIndex = updatedText.IndexOf("Load(ref int value)", StringComparison.Ordinal);
        var outIndex = updatedText.IndexOf("Load(out int value)", StringComparison.Ordinal);
        var inIndex = updatedText.IndexOf("Load(in int value)", StringComparison.Ordinal);
        Assert.True(valueIndex >= 0);
        Assert.True(refIndex >= 0);
        Assert.True(outIndex >= 0);
        Assert.True(inIndex >= 0);
        Assert.True(valueIndex < refIndex);
        Assert.True(refIndex < outIndex);
        Assert.True(outIndex < inIndex);
    }

    [Fact]
    public async Task Organizer_Orders_Generic_Overloads_After_Non_Generic_Overloads_With_Same_Parameter_Count()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
                public Task<string> Load<T>(T value) => Task.FromResult(string.Empty);
                public Task<string> Execute(CancellationToken ct) => Task.FromResult(string.Empty);
                public Task<string> Load(string value) => Task.FromResult(string.Empty);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace, source, out var documentId, assemblyName: "SharedKernel.Style.CodeFixes.Tests.Generic");
        var document = Assert.IsType<Document>(project.GetDocument(documentId));
        var root = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(root);
        var targetMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        // Act
        var organizedSolution = await OrganizeOverloads(
            workspace.CurrentSolution,
            documentId,
            targetMethod,
            updatedName: "Load",
            TestContext.Current.CancellationToken);
        var updatedText = await ReadDocumentText(organizedSolution, documentId);

        // Assert
        var nonGenericIndex = updatedText.IndexOf("Load(string value)", StringComparison.Ordinal);
        var genericIndex = updatedText.IndexOf("Load<T>(T value)", StringComparison.Ordinal);
        Assert.True(nonGenericIndex >= 0);
        Assert.True(genericIndex >= 0);
        Assert.True(nonGenericIndex < genericIndex);
    }

    private static Project CreateProject(AdhocWorkspace workspace, string source, out DocumentId documentId, string assemblyName = "SharedKernel.Style.CodeFixes.Tests.Organizer")
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

        return Assert.IsType<Project>(workspace.CurrentSolution.GetProject(projectId));
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        Assert.False(string.IsNullOrWhiteSpace(trustedPlatformAssemblies));
        var trustedAssemblyPaths = Assert.IsType<string>(trustedPlatformAssemblies);

        foreach (var path in trustedAssemblyPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }

    private static async Task<Solution> OrganizeOverloads(
        Solution solution,
        DocumentId documentId,
        MethodDeclarationSyntax targetMethod,
        string updatedName,
        CancellationToken ct)
    {
        var organizerType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("SharedKernel.Style.CodeFixes.MethodOverloadGroupOrganizer");
        Assert.NotNull(organizerType);
        var organizeMethod = organizerType.GetMethod("Organize", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(organizeMethod);
        var task = Assert.IsType<Task<Solution>>(organizeMethod.Invoke(null, [solution, documentId, targetMethod, updatedName, ct]));
        return await task.ConfigureAwait(false);
    }

    private static bool InvokeIsRenamedMethodMatch(IMethodSymbol candidateSymbol, ISymbol originalSymbol, string updatedName)
    {
        var codeFixType = typeof(SharedKernelStyleCodeFixProvider).Assembly.GetType("SharedKernel.Style.CodeFixes.RemoveAsyncSuffixCodeFix");
        Assert.NotNull(codeFixType);
        var method = codeFixType.GetMethod("IsRenamedMethodMatch", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [candidateSymbol, originalSymbol, updatedName]));
    }

    private static async Task<string> ReadDocumentText(Solution solution, DocumentId documentId)
    {
        var document = Assert.IsType<Document>(solution.GetDocument(documentId));
        return (await document.GetTextAsync().ConfigureAwait(false)).ToString();
    }
}
