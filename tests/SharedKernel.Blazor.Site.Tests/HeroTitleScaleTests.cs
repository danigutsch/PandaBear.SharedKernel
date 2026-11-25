namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the size of a hero's title.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class HeroTitleScaleTests : BunitContext
{
    [Fact]
    public void A_hero_title_has_the_regular_scale_and_no_scale_class_by_default()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters.Add(component => component.Title, "Discover"));

        // Assert
        cut.Find("section").ClassName.ShouldBe("site-hero");
    }

    [Theory]
    [InlineData(HeroLayout.Split, CoverHeight.Page, "site-hero site-hero--title-large")]
    [InlineData(HeroLayout.Cover, CoverHeight.Landing, "site-hero site-hero--cover site-hero--landing site-hero--title-large")]
    public void A_large_title_adds_its_class_in_any_layout(HeroLayout layout, CoverHeight height, string classes)
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Layout, layout)
            .Add(component => component.CoverHeight, height)
            .Add(component => component.TitleScale, HeroTitleScale.Large));

        // Assert
        cut.Find("section").ClassName.ShouldBe(classes);
        cut.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void An_undefined_title_scale_is_refused()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.TitleScale, (HeroTitleScale)9));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Hero.TitleScale));
    }

    [Fact]
    public void The_large_title_multiplies_the_level_one_heading_size_by_its_token()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var rule = SiteFiles.Rule(css, ".site-hero--title-large .site-hero__title");

        // Assert
        rule.ShouldContain("font-size: calc(var(--site-h1-font-size, var(--site-font-size-3xl)) * var(--site-hero-title-scale-large));", StringComparison.Ordinal);
        css.ShouldContain("--site-hero-title-scale-large: 1.25;", StringComparison.Ordinal);
    }
}
