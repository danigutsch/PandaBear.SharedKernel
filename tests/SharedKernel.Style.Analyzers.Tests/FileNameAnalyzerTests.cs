namespace PandaBear.Style.Analyzers.Tests;

/// <summary>Tests for the file-name rule SKSTYLE016, which compares a single top-level type with its file name.</summary>
public sealed class FileNameAnalyzerTests
{
    [Fact]
    public async Task Single_type_in_differently_named_file_reports_skstyle016()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Loader.cs");

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("TourLoader.cs", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TourLoader.cs")]
    [InlineData("src/Demo/TourLoader.cs")]
    public async Task Single_type_in_matching_file_does_not_report_skstyle016(string path)
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: path);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Theory]
    [InlineData("Box.cs")]
    [InlineData("Box{T}.cs")]
    public async Task Generic_type_matches_file_named_with_or_without_type_parameters(string path)
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Box<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: path);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Fact]
    public async Task Nested_types_do_not_count_as_top_level_types()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Outer
            {
                public sealed class Inner
                {
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Outer.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Fact]
    public async Task Multiple_top_level_types_do_not_report_skstyle016()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class First
            {
            }

            public sealed class Second
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Other.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Fact]
    public async Task Generated_file_does_not_report_skstyle016()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Loader.g.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Theory]
    [InlineData("BoxOfT.cs")]
    [InlineData("Box{TValue}.cs")]
    public async Task Generic_type_in_file_with_other_type_parameters_reports_skstyle016(string path)
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class Box<T>
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: path);

        // Assert
        diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }

    [Fact]
    public async Task Partial_type_in_aspect_file_does_not_report_skstyle016()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed partial class TourLoader
            {
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "TourLoader.Parsing.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.FileNameMismatch);
    }
}
