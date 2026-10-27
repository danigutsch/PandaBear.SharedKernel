using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Tests the SKTEST016 rule against constructing the system under test in the Act section, and the SKTEST010 rule on activity listener predicates.</summary>
public sealed class TestIsolationDiagnosticTests
{
    [Theory]
    [InlineData("var sut = new Widget();", true)]
    [InlineData("Widget sut = new();", true)]
    [InlineData("var value = new Widget();", false)]
    [InlineData("sut = new Widget();", true)]
    [InlineData("var sut = Build();", false)]
    public async Task Construction_in_explicit_act_reports_only_sut_targets(string statement, bool expected)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(statement);
        var source = $$"""
            public class Widget { }
            public class Tests
            {
                [Fact] public void Builds_a_widget()
                {
                    // Arrange
                    Widget sut = null;
                    // Act
                    {{statement.Replace("var sut", "var systemUnderTest", StringComparison.Ordinal).Replace("Widget sut = new", "Widget systemUnderTest = new", StringComparison.Ordinal)}}
                    // Assert
                }
                private Widget Build() => new();
            }
            """;

        // Act
        var compilationErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.Any(static d => d.Id == "SKTEST016").ShouldBe(expected);
    }

    [Theory]
    [InlineData("// Arrange", "// Act", "// Assert", true)]
    [InlineData("// Arrange", "// Act", "// Assert later", false)]
    [InlineData("// Act", "// Arrange", "// Assert", false)]
    [InlineData("// Arrange", "// Act here", "// Assert", false)]
    public async Task Construction_requires_one_ordered_exact_marker_set(string arrange, string act, string assert, bool expected)
    {
        // Arrange
        var source = $$"""
            public class Widget { }
            public class Tests
            {
                [Fact] public void Builds_a_widget()
                {
                    {{arrange}}
                    {{act}}
                    var sut = new Widget();
                    {{assert}}
                }
            }
            """;

        // Act
        var compilationErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.Any(static d => d.Id == "SKTEST016").ShouldBe(expected);
    }

    [Theory]
    [InlineData("source => source.Name == \"test\"", true)]
    [InlineData("source => true", false)]
    [InlineData("source => \"test\" == source.Name", true)]
    [InlineData("source => string.Equals(source.Name, \"test\", StringComparison.Ordinal)", true)]
    [InlineData("source => source.Name.Equals(\"test\", StringComparison.Ordinal)", true)]
    [InlineData("delegate(ActivitySource source) { return source.Name == \"test\"; }", true)]
    [InlineData("source => source.Name == \"first\" || source.Name == \"second\"", true)]
    [InlineData("source => ReferenceEquals(source, expected)", false)]
    [InlineData("source => object.ReferenceEquals(source, expected)", false)]
    [InlineData("source => source == expected", false)]
    public async Task Listener_predicates_require_source_identity(string predicate, bool expectedDiagnostic)
    {
        // Arrange
        var source = $$"""
            using System.Diagnostics;
            public class Capture
            {
                public void Start(ActivitySource expected)
                {
                    var listener = new ActivityListener { ShouldListenTo = {{predicate}} };
                }
            }
            """;

        // Act
        var compilationErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.Count(static d => d.Id == "SKTEST010").ShouldBe(expectedDiagnostic ? 1 : 0);
    }

    [Theory]
    [InlineData("[Fact]", "// Act", "var sut = new Widget();", true)]
    [InlineData("[Theory]", "// Act", "var sut = new Widget();", true)]
    [InlineData("", "// Act", "var sut = new Widget();", false)]
    [InlineData("[Fact]", "// Act", "Action create = () => { var sut = new Widget(); };", false)]
    [InlineData("[Fact]", "// Act", "var sut = Render<Widget>();", false)]
    [InlineData("[Fact]", "// Act", "var result = new Widget();", false)]
    public async Task Sut_detection_respects_test_and_execution_boundaries(string attribute, string marker, string statement, bool expected)
    {
        // Arrange
        var source = $$"""
            public class Widget { }
            public class Tests
            {
                {{attribute}} public void Builds_a_widget()
                {
                    // Arrange
                    {{marker}}
                    {{statement}}
                    // Assert
                }
                private T Render<T>() => default;
            }
            """;

        // Act
        var compilationErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.Any(static d => d.Id == "SKTEST016").ShouldBe(expected);
    }

    [Theory]
    [InlineData("TestSource.cs", true)]
    [InlineData("TestSource.g.cs", false)]
    public async Task Listener_assignments_support_aliases_and_exclude_generated_code(string path, bool expected)
    {
        // Arrange
        const string source = """
            using Listener = System.Diagnostics.ActivityListener;
            public class Capture
            {
                public void Start()
                {
                    var listener = new Listener();
                    listener.ShouldListenTo = source => source.Name == "test";
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: path);

        // Assert
        diagnostics.Any(static d => d.Id == "SKTEST010").ShouldBe(expected);
    }

    [Fact]
    public async Task Listener_method_groups_and_ordinary_source_name_reads_remain_valid()
    {
        // Arrange
        const string source = """
            using System.Diagnostics;
            public class Capture
            {
                public void Start(ActivitySource source)
                {
                    var listener = new ActivityListener { ShouldListenTo = Matches };
                    var predicate = Matches;
                    listener.ShouldListenTo = predicate;
                    Console.WriteLine(source.Name);
                }
                private static bool Matches(ActivitySource source) => source.Name == "test";
            }
            """;

        // Act
        var compilationErrors = AnalyzerTestHarness.CreateCompilation(source).GetDiagnostics(TestContext.Current.CancellationToken).Where(static d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        compilationErrors.ShouldBeEmpty();
        diagnostics.ShouldNotContain(static d => d.Id == "SKTEST010");
    }
}
