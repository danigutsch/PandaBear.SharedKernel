extern alias testingcodefixes;

using Microsoft.CodeAnalysis;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST020 code fix that replaces an exact-address page wait with ExpectPath.</summary>
public sealed class PlaywrightUrlWaitCodeFixTests
{
    private const string DiagnosticId = TestingDiagnosticIds.PlaywrightUrlWait;

    [Theory]
    [InlineData("page.WaitForURLAsync(\"http://localhost/customers\")", "page.ExpectPath(new Uri(\"http://localhost/customers\"))")]
    [InlineData("page.WaitForURLAsync(new Uri(baseAddress, path).ToString())", "page.ExpectPath(new Uri(baseAddress, path))")]
    [InlineData("page.WaitForURLAsync(baseAddress.ToString())", "page.ExpectPath(baseAddress)")]
    [InlineData("page.WaitForURLAsync($\"https://localhost/customers/{path}\")", "page.ExpectPath(new Uri($\"https://localhost/customers/{path}\"))")]
    public async Task An_exact_address_wait_on_a_page_becomes_an_expect_path_call(string wait, string expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(wait);
        var source = PlaywrightUrlWaitTestSource.Create($"await {wait};");
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, wait);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);
        await workspace.ApplyCodeAction(actions.ShouldHaveSingleItem());
        var updated = await workspace.GetDocumentText();
        var compilerErrors = AnalyzerTestHarness.CreateCompilation(updated).GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error);
        var analyzerDiagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(updated);

        // Assert
        updated.ShouldContain($"await {expected};", StringComparison.Ordinal);
        updated.ShouldContain("using SharedKernel.Testing.Playwright;", StringComparison.Ordinal);
        compilerErrors.ShouldBeEmpty();
        analyzerDiagnostics.ShouldNotContain(static d => d.Id == DiagnosticId);
    }

    [Theory]
    [InlineData("page.WaitForURLAsync(\"**/customers\")")]
    [InlineData("page.WaitForURLAsync(\"http://localhost/customers?id=1\")")]
    [InlineData("page.WaitForURLAsync(\"customers\")")]
    [InlineData("page.WaitForURLAsync(\"/customers\")")]
    [InlineData("page.WaitForURLAsync(\"file:///customers\")")]
    [InlineData("page.WaitForURLAsync($\"/customers/{path}\")")]
    [InlineData("page.WaitForURLAsync($\"{baseAddress}customers\")")]
    [InlineData("page.WaitForURLAsync($\"{path}/customers\")")]
    [InlineData("page.WaitForURLAsync($\"**/{path}\")")]
    [InlineData("page.WaitForURLAsync(path)")]
    [InlineData("page.WaitForURLAsync(\"http://localhost/customers\", new Microsoft.Playwright.PageWaitForURLOptions())")]
    public async Task A_pattern_a_relative_address_or_an_option_is_not_rewritten(string wait)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(wait);
        var source = PlaywrightUrlWaitTestSource.Create($"await {wait};");
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, wait);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task No_fix_is_offered_when_the_expect_path_helper_is_not_referenced()
    {
        // Arrange
        const string wait = "page.WaitForURLAsync(\"http://localhost/customers\")";
        var source = PlaywrightUrlWaitTestSource.Create($"await {wait};", withExpectPath: false);
        var workspace = CodeFixTestWorkspace.Create(source);
        var diagnostic = await workspace.CreateDocumentDiagnostic(DiagnosticId, wait);
        var provider = new testingcodefixes::SharedKernel.Testing.CodeFixes.TestingCodeFixProvider();

        // Act
        var actions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        actions.ShouldBeEmpty();
    }
}
