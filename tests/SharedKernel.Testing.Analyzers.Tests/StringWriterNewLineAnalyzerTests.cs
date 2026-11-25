using System.Globalization;
using Microsoft.CodeAnalysis;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST022 rule that reports a test StringWriter whose captured text ends lines with the platform line ending.</summary>
public sealed class StringWriterNewLineAnalyzerTests
{
    private const string DiagnosticId = TestingDiagnosticIds.StringWriterNewLine;

    [Theory]
    [InlineData("new StringWriter()")]
    [InlineData("new StringWriter(CultureInfo.InvariantCulture)")]
    [InlineData("new System.IO.StringWriter(builder, CultureInfo.InvariantCulture)")]
    [InlineData("new StringWriter { }")]
    public async Task A_string_writer_without_a_line_ending_is_reported(string creation)
    {
        // Arrange
        var source = StringWriterNewLineTestSource.Create(creation);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            "Use SharedKernel.Testing.InvariantStringWriter instead of StringWriter, or set NewLine in the initializer; the captured text ends lines with the platform line ending");
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe(creation);
    }

    [Fact]
    public async Task A_target_typed_string_writer_is_reported()
    {
        // Arrange
        const string source = """
            namespace Demo
            {
                public sealed class Capture
                {
                    public StringWriter Output { get; } = new();
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        var sourceText = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        sourceText.ToString(diagnostic.Location.SourceSpan).ShouldBe("new()");
    }

    [Theory]
    [InlineData("new StringWriter(CultureInfo.InvariantCulture) { NewLine = \"\\r\\n\" }")]
    [InlineData("new StringWriter { NewLine = \"\\n\" }")]
    [InlineData("new SharedKernel.Testing.InvariantStringWriter()")]
    [InlineData("new ConsoleCapture()")]
    [InlineData("new StreamWriter(new MemoryStream())")]
    public async Task A_writer_with_a_chosen_line_ending_or_another_type_is_not_reported(string creation)
    {
        // Arrange
        var source = StringWriterNewLineTestSource.Create(creation);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsById(DiagnosticId, source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_string_writer_outside_a_test_project_is_not_reported()
    {
        // Arrange
        var compilation = AnalyzerTestHarness.CreateCompilation(StringWriterNewLineTestSource.Create("new StringWriter()"));
        var xunit = compilation.References
            .Where(static reference => Path.GetFileName(reference.Display ?? string.Empty).StartsWith("xunit", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var withoutXunit = compilation.RemoveReferences(xunit);

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(withoutXunit, new TestAnalyzerConfigOptionsProvider(null));

        // Assert
        withoutXunit.GetTypeByMetadataName("Xunit.FactAttribute").ShouldBeNull();
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == DiagnosticId);
    }

    [Fact]
    public void The_rule_is_a_warning_with_a_help_link()
    {
        // Arrange
        var analyzer = new TestingAnalyzer();

        // Act
        var descriptor = analyzer.SupportedDiagnostics.ShouldHaveSingleItem(static rule => rule.Id == DiagnosticId);

        // Assert
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
        descriptor.HelpLinkUri.ShouldEndWith("#sktest022", StringComparison.Ordinal);
    }
}
