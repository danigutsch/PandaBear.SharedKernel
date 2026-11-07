namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Tests that the analyzer reports component loops that render rows, items or components without a key (SKBLZ003) only where intended.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ComponentStateCapability)]
public sealed class KeyedLoopAnalyzerTests
{
    [Theory]
    [InlineData("builder.OpenElement(0, \"tr\");")]
    [InlineData("builder.OpenElement(0, \"li\");")]
    [InlineData("builder.OpenComponent<Row>(0);")]
    public async Task A_loop_that_renders_rows_items_or_components_without_a_key_is_reported(string open)
    {
        // Arrange
        var source = KeyedLoopSources.Page(open, "builder.CloseElement();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(BlazorDiagnosticIds.MissingKey);
        AnalyzerTestHarness.GetReportedLine(diagnostic).ShouldContain(open, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_keyed_loop_is_not_reported()
    {
        // Arrange
        var source = KeyedLoopSources.Page("builder.OpenElement(0, \"tr\");", "builder.SetKey(item);\n            builder.CloseElement();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Options_and_other_elements_are_not_reported()
    {
        // Arrange
        var source = KeyedLoopSources.Page("builder.OpenElement(0, \"option\");", "builder.CloseElement();");

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_loop_in_a_class_that_is_not_a_component_is_not_reported()
    {
        // Arrange
        const string source = """
            using Microsoft.AspNetCore.Components.Rendering;

            namespace Demo;

            public sealed class Renderer
            {
                public void Render(RenderTreeBuilder builder, List<int> items)
                {
                    foreach (var item in items)
                    {
                        builder.OpenElement(0, "tr");
                        builder.CloseElement();
                    }
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
