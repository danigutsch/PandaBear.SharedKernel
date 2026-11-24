namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the colour tokens and rules of the status badge in the stylesheet.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class StatusStylesheetTests
{
    private static readonly string[] ToneNames = ["neutral", "info", "success", "warning", "danger"];

    /// <summary>Gets every tone in the light and the dark scheme.</summary>
    public static TheoryData<string, bool> TonesAndSchemes { get; } = new()
    {
        { "neutral", false },
        { "neutral", true },
        { "info", false },
        { "info", true },
        { "success", false },
        { "success", true },
        { "warning", false },
        { "warning", true },
        { "danger", false },
        { "danger", true },
    };

    [Theory]
    [MemberData(nameof(TonesAndSchemes))]
    public void The_text_of_every_tone_reaches_4_5_to_1_on_its_background_in_both_schemes(string tone, bool dark)
    {
        // Arrange
        var colors = SiteFiles.Colors(dark);

        // Act
        var ratio = SiteFixtures.Contrast(colors[$"color-on-status-{tone}"], colors[$"color-status-{tone}"]);

        // Assert
        ratio.ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData("neutral")]
    [InlineData("info")]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("danger")]
    public void Every_tone_takes_its_background_and_text_from_its_own_tokens(string tone)
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var rule = SiteFiles.Rule(css, $".site-status--{tone}");

        // Assert
        rule.ShouldContain($"background: var(--site-color-status-{tone});", StringComparison.Ordinal);
        rule.ShouldContain($"color: var(--site-color-on-status-{tone});", StringComparison.Ordinal);
    }

    [Fact]
    public void The_tones_are_distinct_colours_so_none_repeats_another()
    {
        // Arrange
        var colors = SiteFiles.Colors(dark: false);

        // Act
        var backgrounds = ToneNames.Select(tone => colors[$"color-status-{tone}"]).ToArray();

        // Assert
        backgrounds.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(backgrounds.Length);
    }

    [Fact]
    public void A_badge_keeps_a_transparent_outline_so_forced_colours_draw_its_edge()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var rule = SiteFiles.Rule(css, ".site-status");

        // Assert
        rule.ShouldContain("outline: 1px solid transparent;", StringComparison.Ordinal);
        rule.ShouldContain("display: inline-block;", StringComparison.Ordinal);
    }
}
