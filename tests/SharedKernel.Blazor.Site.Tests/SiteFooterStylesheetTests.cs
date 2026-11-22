namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the stylesheet rules of the footer tones, the separated list and the tap targets on narrow screens.</summary>
public sealed class SiteFooterStylesheetTests
{
    [Fact]
    public void A_primary_footer_is_a_full_width_band_of_the_primary_colour_in_the_light_scheme()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var rule = SiteFiles.Rule(css, ".site-footer--primary");

        // Assert
        rule.ShouldContain("color-scheme: light", StringComparison.Ordinal);
        rule.ShouldContain("width: 100%", StringComparison.Ordinal);
        rule.ShouldContain("padding: var(--site-space-5) max(var(--site-space-3), (100% - var(--site-content-width)) / 2) var(--site-space-4)", StringComparison.Ordinal);
        rule.ShouldContain("border-top: 0", StringComparison.Ordinal);
        rule.ShouldContain("background: var(--site-color-primary)", StringComparison.Ordinal);
        rule.ShouldContain("color: var(--site-color-on-primary)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-footer--primary a:not(.site-button)").ShouldContain("color: inherit", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-footer--primary :focus-visible").ShouldContain("outline-color: var(--site-color-on-primary)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-footer--text-center").ShouldContain("text-align: center", StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_spaces_its_lines_with_a_gap_and_sets_an_address_upright()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-footer__content").ShouldContain("gap: var(--site-space-2)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ":is(.site-footer__content, .site-footer__legal) > p").ShouldContain("margin: 0", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-footer address").ShouldContain("font-style: normal", StringComparison.Ordinal);
    }

    [Fact]
    public void Separated_items_stay_together_and_the_bar_has_empty_alternative_text()
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var item = SiteFiles.Rule(css, ".site-separated-list__item");
        var bar = SiteFiles.Rule(css, ".site-separated-list__item + .site-separated-list__item::before");

        // Assert
        SiteFiles.Rule(css, ".site-separated-list").ShouldContain("list-style: none", StringComparison.Ordinal);
        item.ShouldContain("display: inline-block", StringComparison.Ordinal);
        item.ShouldContain("overflow-wrap: anywhere", StringComparison.Ordinal);
        bar.ShouldContain("content: \"|\" / \"\";", StringComparison.Ordinal);
    }

    [Fact]
    public void Footer_and_language_links_are_44px_tap_targets_below_the_breakpoint_and_pager_links_always()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var narrow = css[css.IndexOf("@media (width < 48rem) {", StringComparison.Ordinal)..];

        // Assert
        narrow.ShouldStartWith("""
            @media (width < 48rem) {
                .site-footer a,
                .site-language-switcher a {
                    display: inline-flex;
                    align-items: center;
                    min-block-size: 2.75rem;
                }

                .site-language-switcher a {
                    justify-content: center;
                    min-inline-size: 2.75rem;
                }
            }
            """, StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-pager a").ShouldContain("min-block-size: 2.75rem", StringComparison.Ordinal);
    }
}
