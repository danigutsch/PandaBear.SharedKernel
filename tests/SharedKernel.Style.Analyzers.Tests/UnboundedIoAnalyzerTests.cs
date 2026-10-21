using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers.Tests;

public sealed class UnboundedIoAnalyzerTests
{
    private const string DiagnosticId = "SKSTYLE011";

    [Theory]
    [InlineData("File.ReadAllText(path)")]
    [InlineData("File.ReadAllText(path, System.Text.Encoding.UTF8)")]
    [InlineData("File.ReadAllTextAsync(path)")]
    [InlineData("File.ReadAllTextAsync(path, ct)")]
    [InlineData("File.ReadAllTextAsync(path, System.Text.Encoding.UTF8, ct)")]
    [InlineData("File.ReadAllBytes(path)")]
    [InlineData("File.ReadAllBytesAsync(path)")]
    [InlineData("File.ReadAllBytesAsync(path, ct)")]
    [InlineData("reader.ReadToEnd()")]
    [InlineData("reader.ReadToEndAsync()")]
    [InlineData("reader.ReadToEndAsync(ct)")]
    [InlineData("((TextReader)reader).ReadToEnd()")]
    [InlineData("reader?.ReadToEnd()", ".ReadToEnd()")]
    [InlineData("FileAlias.ReadAllText(path)")]
    [InlineData("ReadAllText(path)")]
    public async Task Enabled_rule_reports_whole_read_invocation_with_limit_guidance(string expression, string? expectedSpan = null)
    {
        // Arrange
        var source = $$"""
            using FileAlias = System.IO.File;
            using static System.IO.File;
            public sealed class Importer
            {
                public object? Execute(string path, StreamReader reader, CancellationToken ct) => {{expression}};
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source, assemblyName: "Production");
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        var diagnostic = diagnostics.Where(static candidate => candidate.Id == DiagnosticId).ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("limit", StringComparison.Ordinal);
        var text = await diagnostic.Location.SourceTree.ShouldNotBeNull().GetTextAsync(TestContext.Current.CancellationToken);
        text.ToString(diagnostic.Location.SourceSpan).ShouldBe(expectedSpan ?? expression);
    }

    [Theory]
    [InlineData("new StringReader(path).ReadToEnd()")]
    [InlineData("new StringReader(path).ReadToEndAsync()")]
    [InlineData("new StringReader(path).ReadToEndAsync(CancellationToken.None)")]
    [InlineData("reader.ReadLine()")]
    [InlineData("reader.Read(new char[32], 0, 32)")]
    [InlineData("File.ReadLines(path)")]
    [InlineData("((dynamic)reader).ReadToEnd()")]
    [InlineData("OtherFile.ReadAllText(path)")]
    public async Task Bounded_incremental_or_unrelated_apis_do_not_report(string expression)
    {
        // Arrange
        var source = $$"""
            public sealed class Importer
            {
                public object? Execute(string path, StreamReader reader) => {{expression}};
            }
            public static class OtherFile
            {
                public static string ReadAllText(string path) => path;
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id is DiagnosticId or "AD0001");
    }

    [Theory]
    [InlineData("TestSource.cs", ReportDiagnostic.Default)]
    [InlineData("Tests.cs", ReportDiagnostic.Suppress)]
    [InlineData("Generated.g.cs", ReportDiagnostic.Warn)]
    public async Task Default_disabled_test_scope_suppression_and_generated_code_are_respected(string path, ReportDiagnostic severity)
    {
        // Arrange
        const string source = """
            public sealed class Importer
            {
                public string Execute(string path) => File.ReadAllText(path);
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source, path: path);
        if (severity != ReportDiagnostic.Default)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = severity }));
        }

        new SharedKernelStyleAnalyzer().SupportedDiagnostics.ShouldHaveSingleItem(static diagnostic => diagnostic.Id == DiagnosticId)
            .IsEnabledByDefault.ShouldBeFalse();

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id is DiagnosticId or "AD0001");
    }

    [Fact]
    public async Task Documented_bound_can_use_standard_suppression_without_disabling_other_rules()
    {
        // Arrange
        const string source = """
            public sealed class Importer
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("IO", "SKSTYLE011", Justification = "Input was copied with an enforced byte limit before construction of this reader.")]
                public string Execute(StreamReader reader) => reader.ReadToEnd();
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id is DiagnosticId or "AD0001");
    }

    [Theory]
    [InlineData("override", true)]
    [InlineData("new", false)]
    public async Task Reader_override_is_reported_but_unrelated_hidden_method_is_excluded(string modifier, bool expectedReport)
    {
        // Arrange
        var source = $$"""
            public sealed class Reader : TextReader
            {
                public {{modifier}} string ReadToEnd() => string.Empty;
            }
            public sealed class Importer
            {
                public string Execute(Reader reader) => reader.ReadToEnd();
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.Any(static diagnostic => diagnostic.Id == DiagnosticId).ShouldBe(expectedReport);
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "AD0001");
    }

    [Theory]
    [InlineData("File", "public static string ReadAllText(string value) => value;", "System.IO.File.ReadAllText(path)")]
    [InlineData("TextReader", "public string ReadToEnd() => string.Empty;", "new System.IO.TextReader().ReadToEnd()")]
    [InlineData("StreamReader", "public string ReadToEnd() => string.Empty;", "new System.IO.StreamReader().ReadToEnd()")]
    public async Task Source_defined_framework_lookalikes_do_not_report_io_diagnostics(string typeName, string member, string expression)
    {
        // Arrange
        var source = $$"""
            namespace System.IO
            {
                public class {{typeName}}
                {
                    {{member}}
                }
            }
            public sealed class Importer
            {
                public object Execute(string path) => {{expression}};
            }
            """;
        var compilation = AnalyzerTestHarness.CreateCompilation(source);
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> { [DiagnosticId] = ReportDiagnostic.Warn }));
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        // Act
        var diagnostics = await compilation.WithAnalyzers([new SharedKernelStyleAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id is DiagnosticId or "AD0001");
    }

}
