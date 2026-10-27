namespace PandaBear.Style.Analyzers.Tests;

public sealed class PropertyDocumentationAnalyzerTests
{
    [Theory]
    [InlineData("internal sealed class Tour { public string Name { get; set; } = \"\"; }")]
    [InlineData("public sealed class Tour { internal string Name { get; set; } = \"\"; }")]
    [InlineData("public sealed class Tour { private string Name { get => \"\"; } }")]
    [InlineData("internal sealed record Tour(string Name);")]
    [InlineData("internal sealed record Tour { public string Name { get; init; } = \"\"; }")]
    public async Task Undocumented_property_outside_cs1591_scope_reports_skstyle017(string source)
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }

    [Theory]
    [InlineData("""
        internal sealed class Tour
        {
            /// <summary>Gets the name.</summary>
            public string Name { get; set; } = "";
        }
        """)]
    [InlineData("""
        internal sealed class Tour
        {
            /** <summary>Gets the name.</summary> */
            public string Name { get; set; } = "";
        }
        """)]
    [InlineData("""
        /// <summary>A tour.</summary>
        /// <param name="Name">The name.</param>
        internal sealed record Tour(string Name);
        """)]
    [InlineData("""
        /// <summary>A tour.</summary>
        /// <param name="Name">The name.</param>
        public sealed record Tour(string Name, int Days);
        """)]
    public async Task Documented_property_does_not_report_skstyle017(string source)
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }

    [Fact]
    public async Task Positional_record_parameter_without_param_documentation_reports_only_that_property()
    {
        // Arrange
        const string source = """
            /// <summary>A tour.</summary>
            /// <param name="Name">The name.</param>
            internal sealed record Tour(string Name, int Days);
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("Days", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_property_is_left_to_cs1591()
    {
        // Arrange
        const string source = "public sealed class Tour { public string Name { get; set; } = \"\"; }";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }

    [Fact]
    public async Task Override_explicit_interface_and_indexer_members_do_not_report_skstyle017()
    {
        // Arrange
        const string source = """
            internal interface IHasName
            {
                string Name { get; }
            }

            internal abstract class Base
            {
                /// <summary>Gets the label.</summary>
                public abstract string Label { get; }
            }

            internal sealed class Tour : Base, IHasName
            {
                public override string Label => "";

                string IHasName.Name => "";

                public int this[int index] => index;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }

    [Fact]
    public async Task Struct_and_interface_properties_are_not_checked()
    {
        // Arrange
        const string source = """
            internal struct Point
            {
                public int X { get; set; }
            }

            internal interface IPoint
            {
                int Y { get; }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }

    [Fact]
    public async Task Generated_file_does_not_report_skstyle017()
    {
        // Arrange
        const string source = "internal sealed class Tour { public string Name { get; set; } = \"\"; }";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, path: "Tour.g.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedProperty);
    }
}
