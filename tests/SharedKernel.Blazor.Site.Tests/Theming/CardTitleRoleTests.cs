using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the card title typography role, which card headings take instead of the section heading roles.</summary>
public sealed class CardTitleRoleTests
{
    [Fact]
    public void The_default_card_title_is_a_large_bold_heading_without_a_transform()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Assert
        css.ShouldContain("--site-card-title-font-size: clamp(1.125rem, 1.0417rem + 0.4167vw, 1.375rem);", StringComparison.Ordinal);
        css.ShouldContain("--site-card-title-font-weight: 700;", StringComparison.Ordinal);
        css.ShouldContain("--site-card-title-line-height: 1.2;", StringComparison.Ordinal);
        css.ShouldNotContain("--site-card-title-text-transform", StringComparison.Ordinal);
        SiteThemeStylesheet.RoleVariables.Count.ShouldBe(13 * 7);
    }

    [Fact]
    public void An_uppercase_section_heading_leaves_the_card_title_in_its_own_case()
    {
        // Arrange
        var h3 = TypographyStyle.Create(transform: ThemeTextTransform.Uppercase).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.H3, h3)).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-h3-text-transform: uppercase;", StringComparison.Ordinal);
        css.ShouldNotContain("--site-card-title-text-transform", StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_title_error_names_its_field_in_camel_case()
    {
        // Arrange
        var tiny = ThemeFixtures.Sized(0.75m);

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.CardTitle, tiny));

        // Assert
        result.FieldErrors().Keys.ShouldBe(["typography.cardTitle.size"]);
    }

    [Fact]
    public void A_card_title_font_reference_is_checked_on_its_field()
    {
        // Arrange
        var style = TypographyStyle.Create(fontKey: "missing").ShouldBeSuccess();

        // Act
        var result = SiteTheme.CreateDraft(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.CardTitle, style));

        // Assert
        result.FieldErrors().Keys.ShouldBe(["typography.cardTitle.fontKey"]);
    }

    [Theory]
    [InlineData("font-family: var(--site-card-title-font-family, var(--site-font-heading))")]
    [InlineData("font-size: var(--site-card-title-font-size, var(--site-font-size-lg))")]
    [InlineData("font-weight: var(--site-card-title-font-weight, 700)")]
    [InlineData("line-height: var(--site-card-title-line-height, 1.2)")]
    [InlineData("letter-spacing: var(--site-card-title-letter-spacing, normal)")]
    [InlineData("text-transform: var(--site-card-title-text-transform, none)")]
    [InlineData("font-style: var(--site-card-title-font-style, normal)")]
    public void The_stylesheet_gives_card_titles_the_role_with_fallbacks_of_their_own(string declaration)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-card__title").ShouldContain(declaration, StringComparison.Ordinal);
    }

    [Fact]
    public void The_preview_card_title_uses_the_card_title_role()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__card-title")
            .ShouldContain("font-size: var(--site-card-title-font-size, inherit)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_editor_model_has_card_title_fields()
    {
        // Arrange & Act
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);

        // Assert
        model.Typography[TypographyRole.CardTitle].SizeMin.ShouldBe(1.125m);
        model.Typography[TypographyRole.CardTitle].Weight.ShouldBe(700);
    }

    [Fact]
    public void A_stored_theme_with_a_display_font_on_its_headings_keeps_it_on_card_titles()
    {
        // Arrange
        var heading = TypographyStyle.Create(fontKey: "display", fallback: GenericFontFamily.Serif, weight: 600, italic: true).ShouldBeSuccess();
        var typography = SiteTypography.Create(new Dictionary<TypographyRole, TypographyStyle>
        {
            [TypographyRole.H1] = heading,
            [TypographyRole.H2] = heading,
            [TypographyRole.H3] = heading,
            [TypographyRole.H4] = heading,
            [TypographyRole.H5] = heading,
            [TypographyRole.H6] = heading,
        });
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography, [ThemeFixtures.Font("display", "Playfair Display")]).ShouldBeSuccess();

        // Act
        var title = theme.Effective(TypographyRole.CardTitle);
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        title.FontKey.ShouldBe("display");
        title.Fallback.ShouldBe(GenericFontFamily.Serif);
        title.Weight.ShouldBe(600);
        title.Italic.ShouldBe(true);
        title.Size.ShouldBe(SiteTheme.Default.Effective(TypographyRole.CardTitle).Size);
        title.Transform.ShouldBeNull();
        css.ShouldContain("--site-card-title-font-family: \"Playfair Display\", Georgia, \"Times New Roman\", Times, serif;", StringComparison.Ordinal);
        css.ShouldContain("--site-card-title-font-weight: 600;", StringComparison.Ordinal);
        css.ShouldContain("--site-card-title-font-style: italic;", StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_title_face_of_its_own_wins_over_the_h3_face()
    {
        // Arrange
        var h3 = TypographyStyle.Create(fontKey: "display", weight: 600).ShouldBeSuccess();
        var title = TypographyStyle.Create(fallback: GenericFontFamily.Monospace, weight: 800).ShouldBeSuccess();
        var typography = SiteTheme.Default.Typography.With(TypographyRole.H3, h3).With(TypographyRole.CardTitle, title);
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography, [ThemeFixtures.Font("display", "Playfair Display")]).ShouldBeSuccess();

        // Act
        var effective = theme.Effective(TypographyRole.CardTitle);

        // Assert
        effective.FontKey.ShouldBeNull();
        effective.Fallback.ShouldBe(GenericFontFamily.Monospace);
        effective.Weight.ShouldBe(800);
    }
}
