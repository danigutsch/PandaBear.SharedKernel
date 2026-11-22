using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the scrim: the least darkening of a photo that keeps the on-primary colour readable on any photo.</summary>
public sealed class SiteScrimTests
{
    [Theory]
    [InlineData("#ffffff", "#1f2937", 4.5, 64)]
    [InlineData("#ffffff", "#1f2937", 3.0, 50)]
    [InlineData("#ffffff", "#000000", 4.5, 54)]
    [InlineData("#ffffff", "#000000", 3.0, 42)]
    [InlineData("#ffffff", "#777777", 3.0, 79)]
    public void The_minimum_percent_is_the_least_whole_percent_that_reaches_the_ratio_over_a_white_photo(string foreground, string scrim, double minimum, int expected)
    {
        // Arrange
        var text = CssColor.FromString(foreground);
        var darkening = CssColor.FromString(scrim);

        // Act
        var percent = SiteScrim.MinimumPercent(text, darkening, minimum);

        // Assert
        percent.ShouldBe(expected);
        SiteScrim.WorstRatio(text, darkening, expected).ShouldBeGreaterThanOrEqualTo(minimum);
        SiteScrim.WorstRatio(text, darkening, expected - 1).ShouldBeLessThan(minimum);
    }

    [Theory]
    [InlineData("#ffffff", "#777777", 4.5)]
    [InlineData("#111827", "#1f2937", 3.0)]
    [InlineData("#111827", "#1f2937", 4.5)]
    public void A_ratio_that_even_an_opaque_scrim_cannot_reach_has_no_percent(string foreground, string scrim, double minimum)
    {
        // Arrange
        var text = CssColor.FromString(foreground);
        var darkening = CssColor.FromString(scrim);

        // Act
        var percent = SiteScrim.MinimumPercent(text, darkening, minimum);

        // Assert
        percent.ShouldBeNull();
        SiteScrim.WorstRatio(text, darkening, 100).ShouldBeLessThan(minimum);
    }

    [Fact]
    public void Dark_text_is_measured_against_a_black_photo_because_darkening_cannot_lift_it()
    {
        // Arrange: dark text on a light scrim colour is readable on white, but a black photo darkened towards #e5e7eb stays dark.
        var text = CssColor.FromString("#111827");
        var darkening = CssColor.FromString("#e5e7eb");

        // Act
        var percent = SiteScrim.MinimumPercent(text, darkening, 4.5);

        // Assert
        percent.ShouldNotBeNull();
        percent.ShouldBe(56);
        SiteScrim.WorstRatio(text, darkening, 0).ShouldBe(1.0);
        SiteScrim.WorstRatio(text, darkening, percent.Value).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact]
    public void The_default_theme_darkens_with_its_text_colour_for_its_on_primary_colour_in_the_light_scheme()
    {
        // Arrange
        var colors = SiteTheme.Default.Colors;

        // Act
        var scrim = SiteScrim.For(colors);

        // Assert
        scrim.Text.ShouldBe(64);
        scrim.LargeText.ShouldBe(50);
        scrim.Boost.ShouldBe(28);
        scrim.IsReachable.ShouldBeTrue();
    }

    [Fact]
    public void The_boost_over_the_large_text_scrim_reaches_at_least_the_text_scrim()
    {
        // Arrange
        var scrim = SiteScrim.For(SiteTheme.Default.Colors);

        // Act
        var combined = 100 - ((100 - scrim.LargeText) * (100 - scrim.Boost) / 100.0);
        var oneLess = 100 - ((100 - scrim.LargeText) * (100 - (scrim.Boost - 1)) / 100.0);

        // Assert
        combined.ShouldBeGreaterThanOrEqualTo(scrim.Text);
        oneLess.ShouldBeLessThan(scrim.Text);
    }

    [Fact]
    public void An_unreachable_scrim_is_opaque_so_a_draft_still_renders_its_best_effort()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.OnPrimary, ThemeFixtures.Pair("#111827", "#111827"));

        // Act
        var scrim = SiteScrim.For(colors);

        // Assert
        scrim.IsReachable.ShouldBeFalse();
        scrim.Text.ShouldBe(100);
        scrim.LargeText.ShouldBe(100);
        scrim.Boost.ShouldBe(0);
    }

    [Fact]
    public void The_theme_stylesheet_renders_the_scrim_tokens_as_percentages()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Assert
        css.ShouldContainInOrder("--site-color-focus: ", "--site-scrim: 64%;", "--site-scrim-large: 50%;", "--site-scrim-boost: 28%;");
        SiteThemeStylesheet.ScrimVariables.ShouldBe(["--site-scrim", "--site-scrim-large", "--site-scrim-boost"]);
    }

    [Fact]
    public void The_scrim_follows_the_theme_colours()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Text, ThemeFixtures.Pair("#000000", "#f3f4f6"));
        var theme = SiteTheme.Create(colors, SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-scrim: 54%;", StringComparison.Ordinal);
        css.ShouldContain("--site-scrim-large: 42%;", StringComparison.Ordinal);
        css.ShouldContain("--site-scrim-boost: 21%;", StringComparison.Ordinal);
    }

    [Fact]
    public void The_stylesheet_defaults_are_the_scrim_of_the_default_theme()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());
        var scrim = SiteScrim.For(SiteTheme.Default.Colors);

        // Act
        var root = SiteFiles.Rule(css, ":root");

        // Assert
        root.ShouldContain($"--site-scrim: {scrim.Text}%;", StringComparison.Ordinal);
        root.ShouldContain($"--site-scrim-large: {scrim.LargeText}%;", StringComparison.Ordinal);
        root.ShouldContain($"--site-scrim-boost: {scrim.Boost}%;", StringComparison.Ordinal);
    }

    [Fact]
    public void An_on_primary_colour_that_no_scrim_makes_readable_is_an_error_on_its_field()
    {
        // Arrange: white on #808080 is 3.94:1, so even an opaque scrim leaves normal text below 4.5:1.
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Text, ThemeFixtures.Pair("#808080", "#f3f4f6"));
        var draft = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        var review = draft.Review();
        var created = SiteTheme.Create(colors, SiteTheme.Default.Typography);

        // Assert
        var check = review.Contrast.Where(check => check.Foreground == ThemeColorRole.OnPrimary && check.Background == ThemeColorRole.Text).ShouldHaveSingleItem();
        check.Scheme.ShouldBe(ColorScheme.Light);
        check.Minimum.ShouldBe(4.5);
        check.Passes.ShouldBeFalse();
        review.Errors.ShouldContain(new ThemeIssue(
            "colors.onPrimary",
            "on-primary over a photo darkened with text reaches at most 3.94:1, with an opaque scrim in the light scheme of photo bands; text needs at least 4.5:1 (WCAG 2.2 SC 1.4.3)."));
        created.FieldErrors()["colors.onPrimary"].ShouldContain(message => message.Contains("photo", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scrim_check_is_made_in_the_light_scheme_only()
    {
        // Arrange & Act
        var contrast = SiteTheme.Default.Review().Contrast;

        // Assert
        contrast.Where(check => check.Foreground == ThemeColorRole.OnPrimary && check.Background == ThemeColorRole.Text)
            .Select(check => check.Scheme).ShouldBe([ColorScheme.Light]);
    }
}
