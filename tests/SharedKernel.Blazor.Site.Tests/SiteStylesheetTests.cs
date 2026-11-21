using System.Text.RegularExpressions;
using PandaBear.Blazor.Site.Theming;

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
        SiteFiles.Rule(css, heading).ShouldContain($"font-size: var(--site-{heading}-font-size, var({token}))", StringComparison.Ordinal);
    }

    [Fact]
    public void Stylesheet_consumes_every_per_role_theme_variable_with_a_fallback()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var missing = SiteThemeStylesheet.RoleVariables
            .Where(variable => !css.Contains("var(" + variable + ", ", StringComparison.Ordinal))
            .ToArray();

        // Assert
        missing.ShouldBe([]);
        SiteThemeStylesheet.RoleVariables.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("h1", "font-family: var(--site-h1-font-family, var(--site-font-heading))")]
    [InlineData("h1", "font-weight: var(--site-h1-font-weight, 700)")]
    [InlineData("h1", "line-height: var(--site-h1-line-height, 1.2)")]
    [InlineData("h3", "font-size: var(--site-h3-font-size, var(--site-font-size-xl))")]
    [InlineData("body", "font-family: var(--site-body-font-family, var(--site-font-body))")]
    [InlineData("body", "font-size: var(--site-body-font-size, var(--site-font-size-base))")]
    [InlineData("body", "line-height: var(--site-body-line-height, var(--site-line-height))")]
    [InlineData("small", "font-size: var(--site-small-font-size, var(--site-font-size-sm))")]
    [InlineData("a", "font-weight: var(--site-link-font-weight, inherit)")]
    [InlineData(".site-button", "font-weight: var(--site-button-font-weight, 600)")]
    [InlineData(".site-nav a", "font-size: var(--site-nav-font-size, inherit)")]
    public void Per_role_variables_fall_back_to_the_values_used_before_themes(string selector, string declaration)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, selector).ShouldContain(declaration, StringComparison.Ordinal);
    }

    [Fact]
    public void Links_inside_headings_keep_the_heading_typography()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = SiteFiles.Rule(css, ":is(h1, h2, h3, h4, h5, h6) a");

        // Assert
        rule.ShouldContain("font: inherit", StringComparison.Ordinal);
        rule.ShouldContain("letter-spacing: inherit", StringComparison.Ordinal);
        rule.ShouldContain("text-transform: inherit", StringComparison.Ordinal);
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
    public void Navigation_groups_show_their_own_disclosure_marker_that_turns_when_open()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-nav__group > summary").ShouldContain("list-style: none", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-nav__group > summary::-webkit-details-marker").ShouldContain("display: none", StringComparison.Ordinal);
        var marker = SiteFiles.Rule(css, ".site-nav__group > summary::after");
        marker.ShouldContain("content: \"\"", StringComparison.Ordinal);
        marker.ShouldContain("border-right: 2px solid currentColor", StringComparison.Ordinal);
        marker.ShouldContain("border-bottom: 2px solid currentColor", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-nav__group[open] > summary::after").ShouldContain("rotate(-135deg)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_group_of_the_current_page_is_styled_like_the_current_link()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var selectors = Regex.Match(css, @"^\.site-nav a\[aria-current=""page""\][^{]*\{", RegexOptions.Multiline, TimeSpan.FromSeconds(1)).Value;

        // Assert
        selectors.ShouldContain(".site-nav__group > summary[aria-current=\"true\"]", StringComparison.Ordinal);
    }

    [Fact]
    public void An_open_navigation_group_stacks_the_navigation_below_the_breakpoint()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-nav:has(.site-nav__group[open])").ShouldContain("flex-basis: 100%", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-nav__list:has(> .site-nav__item > .site-nav__group[open])").ShouldContain("flex-direction: column", StringComparison.Ordinal);
        css.ShouldContain("""
                .site-nav:has(.site-nav__group[open]) {
                    flex-basis: auto;
                }
            """, StringComparison.Ordinal);
        css.ShouldContain("""
                .site-nav__list:has(> .site-nav__item > .site-nav__group[open]) {
                    flex-direction: row;
                }
            """, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_grid_keeps_empty_columns_so_a_lone_card_has_the_width_of_one_column()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-card-grid").ShouldContain("grid-template-columns: repeat(auto-fill, minmax(min(100%, var(--site-card-min-width)), 1fr))", StringComparison.Ordinal);
    }

    [Fact]
    public void The_gutter_container_shares_the_content_width_rule_and_the_readme_documents_it()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var readme = File.ReadAllText(SiteFiles.ReadmePath());

        // Act
        var selectors = Regex.Match(css, @"^\.site-header__inner,[^{]*\{", RegexOptions.Multiline, TimeSpan.FromSeconds(1)).Value;

        // Assert
        selectors.ShouldContain(".site-container", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-header__inner").ShouldNotContain("padding", StringComparison.Ordinal);
        readme.ShouldContain("`site-container`", StringComparison.Ordinal);
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

    [Fact]
    public void Photo_scrims_use_the_computed_theme_tokens_instead_of_fixed_opacities()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        css.ShouldContain("color-mix(in srgb, var(--site-color-text) var(--site-scrim), transparent)", StringComparison.Ordinal);
        css.ShouldContain("color-mix(in srgb, var(--site-color-text) var(--site-scrim-large), transparent)", StringComparison.Ordinal);
        css.ShouldContain("color-mix(in srgb, var(--site-color-text) var(--site-scrim-boost), transparent)", StringComparison.Ordinal);
        var mixes = MixPattern().Matches(css).Select(match => match.Groups["amount"].Value).ToArray();
        mixes.Length.ShouldBe(css.Split("color-mix(").Length - 1);
        mixes.ShouldAllSatisfy(amount => amount.ShouldStartWith("var(--site-scrim", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(".site-band--primary")]
    [InlineData(".site-hero--cover")]
    public void Bands_on_the_primary_colour_or_a_photo_use_the_light_scheme_and_the_on_primary_colour(string selector)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = SiteFiles.Rule(css, selector);

        // Assert
        rule.ShouldContain("color-scheme: light", StringComparison.Ordinal);
        rule.ShouldContain("background: var(--site-color-primary)", StringComparison.Ordinal);
        rule.ShouldContain("color: var(--site-color-on-primary)", StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_split_moves_its_media_only_visually_and_only_from_the_breakpoint_on()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        css.ShouldContain("""
                .site-split--media-start > .site-split__media {
                    order: -1;
                }
            """, StringComparison.Ordinal);
        Regex.IsMatch(css, @"^\.site-split--media-start", RegexOptions.Multiline, TimeSpan.FromSeconds(1)).ShouldBeFalse();
        css.ShouldNotContain("row-reverse", StringComparison.Ordinal);
    }

    [Fact]
    public void Fact_grid_column_modifiers_set_equal_columns_only_from_the_breakpoint_on()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var modifiers = Regex.Matches(css, @"site-fact-grid--columns-(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1)).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var breakpoint = css.IndexOf("@media (min-width: 48rem) {", StringComparison.Ordinal);
        var breakpointEnd = css.IndexOf("\n}", breakpoint, StringComparison.Ordinal);

        // Assert
        modifiers.ShouldBe(["1", "2", "3", "4"]);
        foreach (var columns in modifiers)
        {
            css.ShouldContain($$"""
                    .site-fact-grid--columns-{{columns}} {
                        grid-template-columns: repeat({{columns}}, minmax(0, 1fr));
                    }
                """, StringComparison.Ordinal);
        }

        Regex.IsMatch(css, @"^\.site-fact-grid--columns", RegexOptions.Multiline, TimeSpan.FromSeconds(1)).ShouldBeFalse();
        css.IndexOf(".site-fact-grid--columns-", StringComparison.Ordinal).ShouldBeGreaterThan(breakpoint);
        css.LastIndexOf(".site-fact-grid--columns-", StringComparison.Ordinal).ShouldBeLessThan(breakpointEnd);
    }

    [Fact]
    public void A_fact_label_takes_the_on_primary_colour_on_the_primary_colour_or_a_photo()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = Regex.Match(css, @"^:is\(\.site-band--primary, \.site-band--image, \.site-hero--cover\) :is\(h1(?<list>[^{]*)\{(?<body>[^}]*)\}", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

        // Assert
        rule.Success.ShouldBeTrue();
        rule.Groups["list"].Value.ShouldContain(".site-fact-grid__label", StringComparison.Ordinal);
        rule.Groups["body"].Value.ShouldContain("color: inherit", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"color-mix\(in srgb, var\(--site-color-[a-z-]+\) (?<amount>[^,]+), transparent\)")]
    private static partial Regex MixPattern();

    [GeneratedRegex(@"--site-([a-z0-9-]+)\s*:")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"`--site-([a-z0-9-]+)`")]
    private static partial Regex DocumentedPattern();
}
