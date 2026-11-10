using System.Text.RegularExpressions;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the static web asset stylesheet and the documented design tokens.</summary>
public sealed partial class SiteStylesheetTests
{
    private const string DarkMedia = "prefers-color-scheme: dark";

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
        var defined = TokenPattern().Matches(css[..css.IndexOf(DarkMedia, StringComparison.Ordinal)]).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
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
    public void Dark_scheme_overrides_only_tokens_that_exist_in_the_default_scheme()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var split = css.IndexOf(DarkMedia, StringComparison.Ordinal);
        var light = TokenPattern().Matches(css[..split]).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        // Act
        var dark = TokenPattern().Matches(css[split..]).Select(match => match.Groups[1].Value).ToArray();

        // Assert
        dark.ShouldNotBeEmpty();
        dark.ShouldAllSatisfy(token => light.ShouldContain(token));
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
        ];

        // Act
        var ratios = pairs.Select(pair => (pair, Ratio: SiteFixtures.Contrast(colors[pair.Foreground], colors[pair.Background]))).ToArray();

        // Assert
        ratios.ShouldAllSatisfy(entry => entry.Ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{entry.pair.Foreground} on {entry.pair.Background}"));
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
