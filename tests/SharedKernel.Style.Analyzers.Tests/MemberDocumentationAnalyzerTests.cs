using System.Collections.Immutable;
using System.Globalization;

namespace PandaBear.Style.Analyzers.Tests;

public sealed class MemberDocumentationAnalyzerTests
{
    [Theory]
    [InlineData("internal sealed class Tour { }", "Type 'Tour'")]
    [InlineData("internal sealed record Tour;", "Type 'Tour'")]
    [InlineData("internal struct Tour { }", "Type 'Tour'")]
    [InlineData("internal interface ITour { }", "Type 'ITour'")]
    [InlineData("internal enum TourKind { }", "Type 'TourKind'")]
    [InlineData("internal delegate void TourHandler();", "Type 'TourHandler'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { internal sealed class Stop { } }", "Type 'Stop'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { public string Name { get; set; } = \"\"; }", "Property 'Name'")]
    [InlineData("/// <summary>T.</summary>\npublic sealed class Tour { internal string Name { get; set; } = \"\"; }", "Property 'Name'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed record Tour { public string Name { get; init; } = \"\"; }", "Property 'Name'")]
    [InlineData("/// <summary>T.</summary>\n/// <param name=\"Name\">N.</param>\ninternal sealed record Tour(string Name, int Days);", "Property 'Days'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { public void Run() { } }", "Method 'Run'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { internal Tour() { } }", "Constructor 'Tour'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { internal int Days; }", "Field 'Days'")]
    [InlineData("/// <summary>T.</summary>\ninternal sealed class Tour { internal event System.EventHandler? Changed; }", "Event 'Changed'")]
    [InlineData("/// <summary>T.</summary>\ninternal enum Kind { A }", "Field 'A'")]
    [InlineData("/// <summary>T.</summary>\ninternal interface ITour { void Run(); }", "Method 'Run'")]
    [InlineData("/// <summary>T.</summary>\ninternal struct Point { public int X { get; set; } }", "Property 'X'")]
    public async Task Undocumented_internal_symbol_reports_skstyle017(string source, string expected)
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldContain(candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember
            && candidate.GetMessage(CultureInfo.InvariantCulture).Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""
        /// <summary>A tour.</summary>
        internal sealed class Tour
        {
            /// <summary>Creates a tour.</summary>
            internal Tour() { }

            /// <summary>Gets the name.</summary>
            public string Name { get; set; } = "";

            /** <summary>Gets the days.</summary> */
            public int Days { get; set; }

            /// <summary>Runs.</summary>
            public void Run() { }

            /// <summary>Count.</summary>
            internal int Count;

            /// <summary>Raised.</summary>
            internal event System.EventHandler? Changed;

            /// <summary>Stop.</summary>
            internal sealed class Stop { }
        }
        """)]
    [InlineData("""
        /// <summary>A tour.</summary>
        /// <param name="Name">The name.</param>
        internal sealed record Tour(string Name);
        """)]
    [InlineData("""
        /// <summary>Kind.</summary>
        internal enum Kind
        {
            /// <summary>A.</summary>
            A,
        }
        """)]
    [InlineData("""
        /// <summary>A tour.</summary>
        internal sealed class Tour
        {
            /// <inheritdoc />
            public override string ToString() => "";

            private string Hidden { get; set; } = "";

            private void Quiet() { }

            private int _count;
        }
        """)]
    public async Task Documented_internal_symbols_do_not_report_skstyle017(string source)
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Public_symbols_are_left_to_cs1591()
    {
        // Arrange
        const string source = "public sealed class Tour { public string Name { get; set; } = \"\"; public void Run() { } }";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Private_nested_type_is_not_checked()
    {
        // Arrange
        const string source = "/// <summary>T.</summary>\npublic sealed class Outer { private sealed class Inner { public int X { get; set; } } }";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Override_explicit_interface_indexer_and_accessor_members_do_not_report_skstyle017()
    {
        // Arrange
        const string source = """
            /// <summary>Has a name.</summary>
            internal interface IHasName
            {
                /// <summary>Gets the name.</summary>
                string Name { get; }
            }

            /// <summary>Base.</summary>
            internal abstract class Base
            {
                /// <summary>Gets the label.</summary>
                public abstract string Label { get; }
            }

            /// <summary>A tour.</summary>
            internal sealed class Tour : Base, IHasName
            {
                /// <summary>Gets the label.</summary>
                public override string Label => "";

                string IHasName.Name => "";

                /// <summary>Indexer.</summary>
                public int this[int index] => index;
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Partial_type_is_documented_when_any_part_is_documented()
    {
        // Arrange
        const string source = "/// <summary>A tour.</summary>\ninternal partial class Tour { }\ninternal partial class Tour { }";

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Generated_file_does_not_report_skstyle017()
    {
        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics("internal sealed class Tour { public string Name { get; set; } = \"\"; }", path: "Tour.g.cs");

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Theory]
    [InlineData("[Fact]")]
    [InlineData("[Theory]\n    [InlineData(1)]")]
    public async Task Test_methods_are_not_checked(string attributes)
    {
        // Arrange
        var source = $$"""
            /// <summary>Tests.</summary>
            internal sealed class TourTests
            {
                {{attributes}}
                public void Run(int value = 0) { }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldNotContain(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
    }

    [Fact]
    public async Task Public_option_reports_public_members_but_still_skips_test_methods()
    {
        // Arrange
        const string source = """
            /// <summary>Tests.</summary>
            public sealed class TourTests
            {
                [Fact]
                public void Run() { }

                public void Helper() { }
            }
            """;
        var options = ImmutableDictionary<string, string>.Empty.Add("sharedkernel_style_document_public_members", "true");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source, options);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem(static candidate => candidate.Id == StyleDiagnosticIds.UndocumentedMember);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("Helper", StringComparison.Ordinal);
    }
}
