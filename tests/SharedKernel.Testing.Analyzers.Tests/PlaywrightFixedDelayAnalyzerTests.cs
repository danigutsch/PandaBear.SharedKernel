using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static PandaBear.Testing.Analyzers.Tests.PlaywrightFixedDelayTestSource;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the opt-in diagnostic for fixed Playwright page and frame delays.</summary>
public sealed class PlaywrightFixedDelayAnalyzerTests
{
    [Theory]
    [InlineData("page.WaitForTimeoutAsync(1000)", "IPage")]
    [InlineData("frame.WaitForTimeoutAsync(duration)", "IFrame")]
    [InlineData("page.MainFrame.WaitForTimeoutAsync(timeout: 500)", "IFrame")]
    public async Task An_enabled_rule_reports_the_bound_browser_delay(string call, string owner)
    {
        // Arrange
        var compilation = Compile($"await {call};");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var errors = compilation.GetDiagnostics(cancellationToken).Where(static item => item.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new TestingAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);

        // Assert
        errors.ShouldBeEmpty();
        var diagnostic = diagnostics.ShouldHaveSingleItem(static item => item.Id == DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain($"{owner}.WaitForTimeoutAsync", StringComparison.Ordinal);
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(cancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe(call);
    }

    [Theory]
    [InlineData("await fake.WaitForTimeoutAsync(duration);", "Microsoft.Playwright")]
    [InlineData("await page.WaitForTimeoutAsync(duration);", "BrowserFixture")]
    [InlineData("await page.Locator(\"#ready\").WaitForAsync(new() { Timeout = duration });", "Microsoft.Playwright")]
    [InlineData("await frame.WaitForFunctionAsync(\"() => Boolean(document.body)\", options: new() { Timeout = duration });", "Microsoft.Playwright")]
    [InlineData("page.SetDefaultTimeout(duration);", "Microsoft.Playwright")]
    [InlineData("await Task.Delay(TimeSpan.FromMilliseconds(duration), cancellationToken);", "Microsoft.Playwright")]
    public async Task Other_operations_are_allowed(string statement, string playwrightAssembly)
    {
        // Arrange
        var compilation = Compile(statement, playwrightAssembly: playwrightAssembly);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var errors = compilation.GetDiagnostics(cancellationToken).Where(static item => item.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new TestingAnalyzer()]).GetAnalyzerDiagnosticsAsync(cancellationToken);

        // Assert
        errors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static item => item.Id == DiagnosticId);
    }

    [Theory]
    [InlineData(null, "TestSource.cs")]
    [InlineData(ReportDiagnostic.Suppress, "TestSource.cs")]
    [InlineData(ReportDiagnostic.Warn, "TestSource.g.cs")]
    public async Task The_rule_respects_default_suppression_and_generated_code(ReportDiagnostic? severity, string path)
    {
        // Arrange
        var compilation = Compile("await page.WaitForTimeoutAsync(1000);", severity, path);
        var analyzer = new TestingAnalyzer();

        // Act
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static item => item.Id == DiagnosticId);
        var descriptor = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(static item => item.Id == DiagnosticId);
        descriptor.IsEnabledByDefault.ShouldBeFalse();
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.HelpLinkUri.ShouldEndWith("#sktest024", StringComparison.Ordinal);
    }
}
