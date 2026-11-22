using PandaBear.Blazor.Site.Theming;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the colour of the heading roles and the card title role.</summary>
public sealed class HeadingColorTests
{
    [Theory]
    [InlineData(ThemeColorRole.Text)]
    [InlineData(ThemeColorRole.Primary)]
    [InlineData(ThemeColorRole.Muted)]
    public void A_style_takes_a_colour_whose_contrast_the_theme_checks(ThemeColorRole color)
    {
        // Arrange & Act
        var style = TypographyStyle.Create(color: color).ShouldBeSuccess();

        // Assert
        style.Color.ShouldBe(color);
    }

    [Theory]
    [InlineData(ThemeColorRole.Accent)]
    [InlineData(ThemeColorRole.OnPrimary)]
    [InlineData((ThemeColorRole)40)]
    public void Any_other_colour_is_an_error_on_the_colour_field(ThemeColorRole color)
    {
        // Arrange & Act
        var result = TypographyStyle.Create(color: color);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors()["color"].ShouldBe(["The colour must be the text, primary or muted colour, whose contrast the theme checks."]);
    }

    [Fact]
    public void A_heading_colour_is_rendered_as_the_role_colour_property()
    {
        // Arrange
        var h2 = TypographyStyle.Create(color: ThemeColorRole.Primary).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.H2, h2)).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-h2-color: var(--site-color-primary);", StringComparison.Ordinal);
        css.ShouldNotContain("--site-h1-color", StringComparison.Ordinal);
        theme.Effective(TypographyRole.H2).Size.ShouldBe(SiteTheme.Default.Effective(TypographyRole.H2).Size);
        SiteThemeStylesheet.HeadingColorVariables.ShouldAllSatisfy(variable => SiteThemeStylesheet.Render(SiteTheme.Default).ShouldNotContain(variable, StringComparison.Ordinal));
    }

    [Fact]
    public void A_card_title_takes_a_colour_of_its_own()
    {
        // Arrange
        var title = TypographyStyle.Create(color: ThemeColorRole.Primary).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.CardTitle, title)).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-card-title-color: var(--site-color-primary);", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypographyRole.Body, "typography.body.color")]
    [InlineData(TypographyRole.Link, "typography.link.color")]
    [InlineData(TypographyRole.Button, "typography.button.color")]
    public void Only_headings_and_card_titles_take_a_colour(TypographyRole role, string field)
    {
        // Arrange
        var style = TypographyStyle.Create(color: ThemeColorRole.Primary).ShouldBeSuccess();

        // Act
        var result = SiteTheme.CreateDraft(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(role, style));

        // Assert
        result.FieldErrors()[field].ShouldBe(["Only headings and card titles take a colour; the components colour this text."]);
    }

    [Fact]
    public void The_stylesheet_colours_each_heading_and_the_card_title_by_its_role_with_a_fallback()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var missing = SiteThemeStylesheet.HeadingColorVariables
            .Where(variable => !css.Contains("color: var(" + variable + ", inherit)", StringComparison.Ordinal))
            .ToArray();

        // Assert
        SiteThemeStylesheet.HeadingColorVariables.ShouldBe(["--site-h1-color", "--site-h2-color", "--site-h3-color", "--site-h4-color", "--site-h5-color", "--site-h6-color", "--site-card-title-color"]);
        missing.ShouldBe([]);
        SiteFiles.Rule(css, "h2").ShouldContain("color: var(--site-h2-color, inherit)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-card__title").ShouldContain("color: var(--site-card-title-color, inherit)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_editor_model_carries_a_heading_colour()
    {
        // Arrange
        var h2 = TypographyStyle.Create(color: ThemeColorRole.Primary).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.H2, h2)).ShouldBeSuccess();
        var invalid = ThemeEditorModel.FromTheme(SiteTheme.Default);
        invalid.Typography[TypographyRole.H3].Color = ThemeColorRole.Accent;

        // Act
        var model = ThemeEditorModel.FromTheme(theme);
        var rebuilt = model.ToTheme().ShouldBeSuccess();

        // Assert
        model.Typography[TypographyRole.H2].Color.ShouldBe(ThemeColorRole.Primary);
        rebuilt.ShouldBe(theme);
        invalid.ToDraft().FieldErrors().Keys.ShouldBe(["typography.h3.color"]);
    }

    [Fact]
    public void The_preview_colours_its_sample_headings_and_card_title_by_their_roles()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__h2").ShouldContain("color: var(--site-h2-color, inherit)", StringComparison.Ordinal);
        SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__card-title").ShouldContain("color: var(--site-card-title-color, inherit)", StringComparison.Ordinal);
    }
}
