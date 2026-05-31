using Microsoft.CodeAnalysis.CodeFixes;
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
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentTextAsync();

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
        var diagnostic = await workspace.CreateDocumentDiagnostic(global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix, "LoadAsync");

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
        Assert.Equal([global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix], diagnosticIds);
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
        Assert.Equal([global::SharedKernel.Style.Analyzers.StyleDiagnosticIds.AsyncSuffix], supportedDiagnosticIds);
    }
}
