using System.Globalization;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST020 rule that reports Playwright's racy <c>WaitForURLAsync</c> on pages and frames.</summary>
public sealed class PlaywrightUrlWaitAnalyzerTests
{
    private const string DiagnosticId = TestingDiagnosticIds.PlaywrightUrlWait;

    [Theory]
    [InlineData("await page.WaitForURLAsync(\"http://localhost/customers\");", "IPage")]
    [InlineData("await page.WaitForURLAsync(\"**/customers\", new Microsoft.Playwright.PageWaitForURLOptions());", "IPage")]
    [InlineData("await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(\"customers$\"));", "IPage")]
    [InlineData("await page.WaitForURLAsync(url => url.EndsWith(\"customers\", StringComparison.Ordinal));", "IPage")]
    [InlineData("await frame.WaitForURLAsync(\"**/customers\");", "IFrame")]
    [InlineData("await page.MainFrame.WaitForURLAsync(\"**/customers\");", "IFrame")]
    public async Task A_page_or_frame_url_wait_is_reported(string statement, string owner)
    {
        // Arrange
        var source = PlaywrightUrlWaitTestSource.Create(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"Use ExpectPath instead of {owner}.WaitForURLAsync, which loses a navigation that lands between its address check and its event subscription");
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe(statement["await ".Length..].TrimEnd(';'));
    }

    [Theory]
    [InlineData("await router.WaitForURLAsync(\"http://localhost/customers\");")]
    [InlineData("await page.ExpectPath(baseAddress, path);")]
    public async Task Other_waits_are_not_reported(string statement)
    {
        // Arrange
        var source = PlaywrightUrlWaitTestSource.Create(statement);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void The_rule_is_a_warning_with_a_help_link()
    {
        // Arrange
        var analyzer = new SharedKernelTestingAnalyzer();

        // Act
        var descriptor = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(static rule => rule.Id == DiagnosticId);

        // Assert
        descriptor.DefaultSeverity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        descriptor.HelpLinkUri.ShouldEndWith("#sktest020", StringComparison.Ordinal);
    }
}
