namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the column widths and the gap of a card grid.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class CardGridOptionsTests : BunitContext
{
    [Fact]
    public void A_default_grid_has_no_modifier_class()
    {
        // Arrange & Act
        var cut = Render<CardGrid>();

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-card-grid");
    }

    [Theory]
    [InlineData(CardColumnWidth.Medium, null, SiteSpace.Space4, "site-card-grid site-card-grid--min-medium")]
    [InlineData(CardColumnWidth.Medium, CardColumnWidth.Wide, SiteSpace.Space6, "site-card-grid site-card-grid--min-medium site-card-grid--max-wide site-card-grid--gap-6")]
    [InlineData(CardColumnWidth.Narrow, CardColumnWidth.Narrow, SiteSpace.Space1, "site-card-grid site-card-grid--max-narrow site-card-grid--gap-1")]
    [InlineData(CardColumnWidth.Narrow, null, SiteSpace.None, "site-card-grid site-card-grid--gap-0")]
    public void Column_widths_and_gap_are_modifier_classes(CardColumnWidth min, CardColumnWidth? max, SiteSpace gap, string expected)
    {
        // Arrange & Act
        var cut = Render<CardGrid>(parameters => parameters
            .Add(component => component.MinColumnWidth, min)
            .Add(component => component.MaxColumnWidth, max)
            .Add(component => component.Gap, gap)
            .Add(component => component.Class, "extra"));

        // Assert
        cut.Find("div").ClassName.ShouldBe(expected + " extra");
    }

    [Fact]
    public void Centred_items_add_their_modifier_class()
    {
        // Arrange & Act
        var cut = Render<CardGrid>(parameters => parameters.Add(component => component.ItemAlignment, SiteAlignment.Center));

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-card-grid site-card-grid--items-center");
    }

    [Theory]
    [InlineData(CardColumnWidth.Wide, CardColumnWidth.Medium)]
    [InlineData(CardColumnWidth.Medium, CardColumnWidth.Narrow)]
    [InlineData(CardColumnWidth.Wide, CardColumnWidth.Narrow)]
    public void A_maximum_below_the_minimum_is_refused(CardColumnWidth min, CardColumnWidth max)
    {
        // Arrange & Act
        var render = () => Render<CardGrid>(parameters => parameters
            .Add(component => component.MinColumnWidth, min)
            .Add(component => component.MaxColumnWidth, max));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(CardGrid.MaxColumnWidth));
    }

    [Fact]
    public void Undefined_options_are_refused()
    {
        // Arrange & Act
        Action[] renders =
        [
            () => Render<CardGrid>(parameters => parameters.Add(component => component.MinColumnWidth, (CardColumnWidth)9)),
            () => Render<CardGrid>(parameters => parameters.Add(component => component.MaxColumnWidth, (CardColumnWidth)9)),
            () => Render<CardGrid>(parameters => parameters.Add(component => component.Gap, (SiteSpace)9)),
            () => Render<CardGrid>(parameters => parameters.Add(component => component.ItemAlignment, (SiteAlignment)9)),
        ];

        // Assert
        renders.ShouldAllSatisfy(render => render.ShouldThrow<ArgumentOutOfRangeException>());
    }
}
