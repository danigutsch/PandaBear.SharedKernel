using Microsoft.CodeAnalysis;

namespace PandaBear.Style.Analyzers.Tests;

public sealed class CaseInsensitiveJsonContextAnalyzerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("[JsonSourceGenerationOptions(WriteIndented = true)]")]
    [InlineData("[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]")]
    [InlineData("[JsonSourceGenerationOptions(JsonSerializerDefaults.General)]")]
    public async Task Enabled_rule_reports_context_without_case_insensitive_property_names(string attribute)
    {
        // Arrange
        var source = CaseInsensitiveJsonContextTestHarness.CreateSource(attribute);

        // Act
        var diagnostics = await CaseInsensitiveJsonContextTestHarness.GetDiagnostics(source, enabled: true);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe("GitHubJsonContext");
    }

    [Theory]
    [InlineData("[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]")]
    [InlineData("[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]")]
    [InlineData("[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]")]
    public async Task Enabled_rule_accepts_context_with_case_insensitive_property_names(string attribute)
    {
        // Arrange
        var source = CaseInsensitiveJsonContextTestHarness.CreateSource(attribute);

        // Act
        var diagnostics = await CaseInsensitiveJsonContextTestHarness.GetDiagnostics(source, enabled: true);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Enabled_rule_ignores_types_that_are_not_serializer_contexts()
    {
        // Arrange
        const string Source = """
            public partial class NotAContext;
            """;

        // Act
        var diagnostics = await CaseInsensitiveJsonContextTestHarness.GetDiagnostics(Source, enabled: true);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rule_is_disabled_by_default_so_local_contract_scopes_stay_untouched()
    {
        // Arrange
        var source = CaseInsensitiveJsonContextTestHarness.CreateSource("");

        // Act
        var diagnostics = await CaseInsensitiveJsonContextTestHarness.GetDiagnostics(source, enabled: false);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
