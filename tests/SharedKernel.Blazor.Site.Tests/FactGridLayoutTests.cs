using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the layouts of the grid of fact cards.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FactGridLayoutTests : BunitContext
{
    private static readonly RenderFragment ClockIcon = builder => builder.AddMarkupContent(0, "<svg id=\"clock\"></svg>");

    private static readonly FactCard[] CardFacts =
    [
        new("Duration", [new("8 days")]),
        new("Distance", [new("320 km")]),
        new("Difficulty", [new("Moderate")]),
    ];

    [Fact]
    public void The_grid_layout_is_the_default_and_keeps_the_balanced_column_class()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, CardFacts));

        // Assert
        cut.Find("dl").ClassName.ShouldBe("site-fact-grid site-fact-grid--columns-3");
    }

    [Fact]
    public void The_inline_layout_is_one_row_without_a_column_class()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, CardFacts)
            .Add(component => component.Layout, FactGridLayout.Inline));

        // Assert
        cut.Find("dl").ClassName.ShouldBe("site-fact-grid site-fact-grid--inline");
    }

    [Fact]
    public void The_inline_layout_keeps_the_same_terms_definitions_and_icons()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, [new FactCard("Duration", [new("8 days")], ClockIcon), CardFacts[1]])
            .Add(component => component.Layout, FactGridLayout.Inline));

        // Assert
        var items = cut.FindAll(".site-fact-grid__item");
        items.Select(item => item.Children.Select(child => child.TagName).ToArray()).ShouldBe([["DT", "DD"], ["DT", "DD"]]);
        items[0].QuerySelector("dt > .site-fact-grid__icon[aria-hidden='true'] > #clock").ShouldNotBeNull();
        items.Select(item => item.TextContent).ShouldBe(["Duration8 days", "Distance320 km"]);
    }

    [Fact]
    public void An_empty_inline_grid_keeps_its_layout_class()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, [])
            .Add(component => component.Layout, FactGridLayout.Inline));

        // Assert
        cut.Find("dl").ClassName.ShouldBe("site-fact-grid site-fact-grid--inline");
    }

    [Fact]
    public void An_undefined_layout_is_refused()
    {
        // Arrange & Act
        var render = () => Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, CardFacts)
            .Add(component => component.Layout, (FactGridLayout)9));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(FactGrid.Layout));
    }
}
