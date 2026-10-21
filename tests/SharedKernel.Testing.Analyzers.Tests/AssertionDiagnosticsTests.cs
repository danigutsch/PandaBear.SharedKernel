using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers.Tests;

public sealed class AssertionDiagnosticsTests
{
    [Theory]
    [InlineData("var value = values.Single();", "SKTEST012")]
    [InlineData("var value = values.Single(item => item > 0);", "SKTEST012")]
    [InlineData("var value = Enumerable.Single(values);", "SKTEST012")]
    [InlineData("var value = values.AsQueryable().Single();", "SKTEST012")]
    [InlineData("var value = values.AsQueryable().Single(item => item > 0);", "SKTEST012")]
    [InlineData("(text).ShouldBe(\"title\");", "SKTEST013")]
    [InlineData("(values.Length > 0).ShouldBeTrue();", "SKTEST013")]
    [InlineData("holder?.Text.ShouldBe(\"title\");", "SKTEST014")]
    [InlineData("holder?.Number.ShouldBe(1);", "SKTEST014")]
    [InlineData("text?.ShouldBe(\"title\");", "SKTEST014")]
    public async Task Unsafe_assertion_forms_report_the_expected_diagnostic(string body, string diagnosticId)
    {
        // Arrange
        var source = AssertionDiagnosticTestSource.Create(body);
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.Where(d => d.Id == diagnosticId).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("var value = values.Single();", false)]
    [InlineData("GC.KeepAlive((values.Length));")]
    [InlineData("var value = holder!.Single();")]
    [InlineData("(holder!).ShouldProceed();")]
    [InlineData("var value = values.ShouldHaveSingleItem();")]
    [InlineData("text.ShouldBe(\"title\");")]
    [InlineData("holder?.ToString();")]
    [InlineData("(holder?.Text).ShouldBe(\"title\");", true, true)]
    [InlineData("Action action = () => values.Single(); action.ShouldThrow<InvalidOperationException>();")]
    [InlineData("((Action)(() => values.Single())).ShouldThrow<InvalidOperationException>();", true, true)]
    public async Task Native_or_deliberate_exception_operations_do_not_report_unsafe_invocations(string body, bool isTest = true, bool parenthesizedAssertionExpected = false)
    {
        // Arrange
        var source = AssertionDiagnosticTestSource.Create(body, isTest);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static d => d.Id is "SKTEST012" or "SKTEST014");
        diagnostics.Any(static d => d.Id == "SKTEST013").ShouldBe(parenthesizedAssertionExpected);
    }

    [Theory]
    [InlineData("ToHaveTitleAsync(\"title\")", true)]
    [InlineData("ToHaveTitleAsync(\"title\", new object())", false)]
    [InlineData("ToHaveURLAsync(\"url\")", false)]
    public async Task Playwright_reports_only_title_calls_with_an_existing_wrapper(string assertion, bool expected)
    {
        // Arrange
        var source = AssertionDiagnosticTestSource.Create($"Microsoft.Playwright.Assertions.Expect(null!).{assertion};") + """
            namespace Microsoft.Playwright
            {
                public interface IPageAssertions
                {
                    Task ToHaveTitleAsync(string title, object? options = null);
                    Task ToHaveURLAsync(string url);
                }
                public static class Assertions
                {
                    public static IPageAssertions Expect(object page) => null!;
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);
        var boundaryDiagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, assemblyName: "SharedKernel.Testing.Playwright");

        // Assert
        diagnostics.Any(static d => d.Id == "SKTEST015").ShouldBe(expected);
        boundaryDiagnostics.ShouldNotContain(static d => d.Id == "SKTEST015");
    }
}
