using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests how the stylesheet applies the theme's shape: control and surface radii, the computed button padding and the dividers.</summary>
public sealed class SiteShapeStylesheetTests
{
    [Theory]
    [InlineData(".site-button")]
    [InlineData(".site-pager a")]
    [InlineData(".site-language-switcher a")]
    public void Controls_take_the_control_radius(string selector)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, selector).ShouldContain("border-radius: var(--site-control-radius)", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".site-card")]
    [InlineData(".site-disclosure")]
    public void Cards_and_disclosures_take_the_surface_radius(string selector)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, selector).ShouldContain("border-radius: var(--site-radius)", StringComparison.Ordinal);
    }

    [Fact]
    public void Button_padding_is_computed_from_the_button_font_size_and_line_height()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var button = SiteFiles.Rule(css, ".site-button");

        // Assert
        SiteThemeStylesheet.ButtonPadding.ShouldBe("max(0.25em, (2.75em - 1lh) / 2) 1.375em");
        button.ShouldContain("padding: " + SiteThemeStylesheet.ButtonPadding + ";", StringComparison.Ordinal);
        button.ShouldNotContain("var(--site-space-", StringComparison.Ordinal);
        button.ShouldContain("min-block-size: 2.75rem", StringComparison.Ordinal);
        button.ShouldContain("display: inline-flex", StringComparison.Ordinal);
    }

    [Fact]
    public void The_section_divider_is_drawn_from_the_divider_tokens()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var divider = SiteFiles.Rule(css, ".site-divider");

        // Assert
        divider.ShouldContain("width: var(--site-divider-width)", StringComparison.Ordinal);
        divider.ShouldContain("height: var(--site-divider-thickness)", StringComparison.Ordinal);
        divider.ShouldContain("background: var(--site-divider-color)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_hero_rule_is_drawn_from_the_same_divider_tokens()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = SiteFiles.Rule(css, ".site-hero--divided .site-hero__eyebrow::after");

        // Assert
        css.ShouldContain(".site-hero--divided .site-hero__eyebrow::after,\n.site-hero--divided .site-hero__subtitle::before {", StringComparison.Ordinal);
        rule.ShouldContain("width: var(--site-divider-width)", StringComparison.Ordinal);
        rule.ShouldContain("height: var(--site-divider-thickness)", StringComparison.Ordinal);
        rule.ShouldContain("background: var(--site-divider-color)", StringComparison.Ordinal);
        rule.ShouldContain("content: \"\"", StringComparison.Ordinal);
        css.ShouldContain(".site-hero--cover.site-hero--divided .site-hero__eyebrow::after,\n.site-hero--cover.site-hero--divided .site-hero__subtitle::before {", StringComparison.Ordinal);
        var cover = SiteFiles.Rule(css, ".site-hero--cover.site-hero--divided .site-hero__eyebrow::after");
        cover.ShouldContain("background: currentColor", StringComparison.Ordinal);
        cover.ShouldContain("margin-inline: auto", StringComparison.Ordinal);
    }
}
