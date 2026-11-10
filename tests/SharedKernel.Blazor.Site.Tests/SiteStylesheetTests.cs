using System.Text.RegularExpressions;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the static web asset stylesheet and the documented design tokens.</summary>
public sealed partial class SiteStylesheetTests
{
    [Fact]
    public void Stylesheet_is_a_static_web_asset_of_the_package_and_the_readme_documents_its_path()
    {
        // Arrange
        var assetPath = Path.Combine(SiteFiles.ProjectDirectory(), "wwwroot", "site.css");

        // Act
        var readme = File.ReadAllText(SiteFiles.ReadmePath());

        // Assert
        File.Exists(assetPath).ShouldBeTrue();
        readme.ShouldContain("_content/PandaBear.SharedKernel.Blazor.Site/site.css", StringComparison.Ordinal);
    }

    [Fact]
    public void Every_documented_token_is_defined_and_every_defined_token_is_documented()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var defined = TokenPattern().Matches(css).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var documented = DocumentedPattern().Matches(File.ReadAllText(SiteFiles.ReadmePath())).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        // Act
        var undocumented = defined.Except(documented, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var undefined = documented.Except(defined, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        // Assert
        undocumented.ShouldBe([]);
        undefined.ShouldBe([]);
        defined.ShouldContain("color-primary");
        defined.ShouldContain("font-body");
        defined.ShouldContain("font-heading");
        defined.ShouldContain("radius");
    }

    [Fact]
    public void Every_colour_token_is_defined_once_for_both_schemes_with_light_dark()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var colorTokens = TokenPattern().Matches(css).Select(match => match.Groups[1].Value).Where(token => token.StartsWith("color-", StringComparison.Ordinal)).ToArray();
        var pairs = SiteFiles.Colors(dark: false);

        // Assert
        colorTokens.ShouldNotBeEmpty();
        colorTokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(colorTokens.Length);
        colorTokens.ShouldAllSatisfy(token => pairs.Keys.ShouldContain(token));
        css.ShouldContain("color-scheme: light dark", StringComparison.Ordinal);
        css.ShouldNotContain("prefers-color-scheme", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Default_text_colours_reach_a_contrast_ratio_of_at_least_4_5_to_1(bool dark)
    {
        // Arrange
        var colors = SiteFiles.Colors(dark);
        (string Foreground, string Background)[] pairs =
        [
            ("color-text", "color-surface"),
            ("color-text", "color-surface-alt"),
            ("color-muted", "color-surface"),
            ("color-muted", "color-surface-alt"),
            ("color-primary", "color-surface"),
            ("color-primary", "color-surface-alt"),
            ("color-accent", "color-surface"),
            ("color-on-primary", "color-primary"),
            ("color-on-accent", "color-accent"),
            ("color-on-primary", "color-primary-hover"),
            ("color-on-accent", "color-accent-hover"),
            ("color-primary-hover", "color-surface"),
            ("color-primary-hover", "color-surface-alt"),
        ];

        // Act
        var ratios = pairs.Select(pair => (pair, Ratio: SiteFixtures.Contrast(colors[pair.Foreground], colors[pair.Background]))).ToArray();

        // Assert
        ratios.ShouldAllSatisfy(entry => entry.Ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{entry.pair.Foreground} on {entry.pair.Background}"));
    }

    [Fact]
    public void Button_hover_uses_the_hover_tokens_instead_of_a_filter()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        css.ShouldNotContain("filter:", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-button--primary:hover").ShouldContain("var(--site-color-primary-hover)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-button--secondary:hover").ShouldContain("var(--site-color-accent-hover)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-button--outline:hover").ShouldContain("var(--site-color-primary-hover)", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("h5", "--site-font-size-base")]
    [InlineData("h6", "--site-font-size-sm")]
    public void Small_headings_are_not_smaller_than_their_documented_size(string heading, string token)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, heading).ShouldContain($"font-size: var({token})", StringComparison.Ordinal);
    }

    [Fact]
    public void Links_in_the_card_body_stay_clickable_above_the_title_link_overlay()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = SiteFiles.Rule(css, ".site-card__body a:not(.site-card__title a)");

        // Assert
        rule.ShouldContain("position: relative", StringComparison.Ordinal);
        rule.ShouldContain("z-index: 1", StringComparison.Ordinal);
        css.ShouldContain(".site-card__title a {", StringComparison.Ordinal);
    }

    [Fact]
    public void Card_shows_its_focus_ring_only_for_keyboard_focus()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        css.ShouldNotContain(".site-card:focus-within", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card:has(:focus-visible)").ShouldContain("outline: var(--site-focus-ring)", StringComparison.Ordinal);
    }

    [Fact]
    public void Stylesheet_honours_reduced_motion_and_keyboard_focus_and_loads_nothing_external()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        css.ShouldContain("prefers-reduced-motion: reduce", StringComparison.Ordinal);
        css.ShouldContain(":focus-visible", StringComparison.Ordinal);
        css.ShouldNotContain("http://", StringComparison.Ordinal);
        css.ShouldNotContain("https://", StringComparison.Ordinal);
        css.ShouldNotContain("@import", StringComparison.Ordinal);
        css.ShouldNotContain("url(", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"--site-([a-z0-9-]+)\s*:")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"`--site-([a-z0-9-]+)`")]
    private static partial Regex DocumentedPattern();
}
