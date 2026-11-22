namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the stylesheet rules of the card options, the card grid widths and gap, and the button sizes.</summary>
public sealed class SiteCardStylesheetTests
{
    [Fact]
    public void A_plain_card_has_no_box_and_takes_the_colour_of_its_surroundings()
    {
        // Arrange & Act
        var rule = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card--plain");

        // Assert
        rule.ShouldContain("background: none", StringComparison.Ordinal);
        rule.ShouldContain("border: 0", StringComparison.Ordinal);
        rule.ShouldContain("overflow: visible", StringComparison.Ordinal);
        rule.ShouldContain("color: inherit", StringComparison.Ordinal);
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card--plain .site-card__media img").ShouldContain("border-radius: var(--site-radius)", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".site-card--aspect-16x9 .site-card__media img", "aspect-ratio: 16 / 9")]
    [InlineData(".site-card--aspect-1x1 .site-card__media img", "aspect-ratio: 1 / 1")]
    [InlineData(".site-card--aspect-natural .site-card__media img", "aspect-ratio: auto")]
    [InlineData(".site-card__media img", "aspect-ratio: 4 / 3")]
    public void Each_image_aspect_sets_the_ratio_of_the_card_image(string selector, string declaration)
    {
        // Arrange & Act & Assert
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), selector).ShouldContain(declaration, StringComparison.Ordinal);
    }

    [Fact]
    public void The_offset_shadow_is_a_hard_block_of_the_border_colour_one_spacing_step_away()
    {
        // Arrange & Act
        var media = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card--image-shadow .site-card__media");
        var image = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card--image-shadow .site-card__media img");

        // Assert
        media.ShouldContain("margin-inline-end: var(--site-space-2)", StringComparison.Ordinal);
        media.ShouldContain("margin-block-end: var(--site-space-2)", StringComparison.Ordinal);
        image.ShouldContain("box-shadow: var(--site-space-2) var(--site-space-2) 0 var(--site-color-border)", StringComparison.Ordinal);
    }

    [Fact]
    public void Long_words_wrap_inside_the_card_and_the_card_never_widens_its_column()
    {
        // Arrange & Act & Assert
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card").ShouldContain("min-width: 0", StringComparison.Ordinal);
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card__body").ShouldContain("overflow-wrap: break-word", StringComparison.Ordinal);
    }

    [Fact]
    public void The_more_look_lets_clicks_through_to_the_title_link_and_hovers_with_it()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var more = SiteFiles.Rule(css, ".site-card__more");

        // Assert
        more.ShouldContain("pointer-events: none", StringComparison.Ordinal);
        css.ShouldContain(".site-button--primary:hover,\n.site-card:has(.site-card__title a:hover) .site-card__more.site-button--primary {", StringComparison.Ordinal);
        css.ShouldContain(".site-button--outline:hover,\n.site-card:has(.site-card__title a:hover) .site-card__more.site-button--outline {", StringComparison.Ordinal);
        css.ShouldContain(".site-button--secondary:hover,\n.site-card:has(.site-card__title a:hover) .site-card__more.site-button--secondary {", StringComparison.Ordinal);
    }

    [Fact]
    public void A_compact_button_is_2_25_times_its_font_size_tall_and_keeps_the_tap_target()
    {
        // Arrange & Act
        var compact = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-button--compact");

        // Assert
        compact.ShouldContain("padding: max(0.25em, (2.25em - 1lh) / 2) 1.125em", StringComparison.Ordinal);
        compact.ShouldNotContain("min-block-size", StringComparison.Ordinal);
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-button").ShouldContain("min-block-size: 2.75rem", StringComparison.Ordinal);
    }

    [Fact]
    public void The_card_grid_columns_and_gap_come_from_tokens_that_the_modifiers_set()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var grid = SiteFiles.Rule(css, ".site-card-grid");

        // Assert
        grid.ShouldContain("grid-template-columns: repeat(auto-fill, minmax(min(100%, var(--site-card-min-width)), var(--site-card-max-width)))", StringComparison.Ordinal);
        grid.ShouldContain("gap: var(--site-card-gap)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card-grid--min-medium").ShouldContain("--site-card-min-width: 22rem", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card-grid--min-wide").ShouldContain("--site-card-min-width: 32rem", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card-grid--max-narrow").ShouldContain("--site-card-max-width: 16rem", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card-grid--max-wide").ShouldContain("--site-card-max-width: 32rem", StringComparison.Ordinal);
        var bounded = SiteFiles.Rule(css, ":is(.site-card-grid--max-narrow, .site-card-grid--max-medium, .site-card-grid--max-wide)");
        bounded.ShouldContain("repeat(auto-fit,", StringComparison.Ordinal);
        bounded.ShouldContain("justify-content: center", StringComparison.Ordinal);
        foreach (var step in Enumerable.Range(1, 6))
        {
            SiteFiles.Rule(css, $".site-card-grid--gap-{step}").ShouldContain($"--site-card-gap: var(--site-space-{step})", StringComparison.Ordinal);
        }

        SiteFiles.Rule(css, ".site-card-grid--gap-0").ShouldContain("--site-card-gap: 0", StringComparison.Ordinal);
    }

    [Fact]
    public void Card_actions_are_one_row_at_the_bottom_of_the_card()
    {
        // Arrange & Act
        var actions = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card .site-card__actions");

        // Assert
        actions.ShouldContain("flex-direction: row", StringComparison.Ordinal);
        actions.ShouldContain("margin-top: auto", StringComparison.Ordinal);
        SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ".site-card--text-center .site-card__actions").ShouldContain("justify-content: center", StringComparison.Ordinal);
    }
}
