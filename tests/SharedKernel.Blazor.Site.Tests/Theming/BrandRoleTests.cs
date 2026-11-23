using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the brand typography role, which a text brand in the header takes instead of the link role.</summary>
public sealed class BrandRoleTests
{
    [Fact]
    public void The_default_brand_is_a_large_bold_heading_face_without_a_transform()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Assert
        css.ShouldContain("--site-brand-font-size: clamp(1.125rem, 1.0417rem + 0.4167vw, 1.375rem);", StringComparison.Ordinal);
        css.ShouldContain("--site-brand-font-weight: 700;", StringComparison.Ordinal);
        css.ShouldContain("--site-brand-line-height: 1.2;", StringComparison.Ordinal);
        css.ShouldNotContain("--site-brand-text-transform", StringComparison.Ordinal);
    }

    [Fact]
    public void The_brand_role_is_named_brand_and_takes_a_colour_without_being_a_heading()
    {
        // Arrange & Act
        var css = ThemeNames.Css(TypographyRole.Brand);
        var field = ThemeNames.Field(TypographyRole.Brand);
        var heading = ThemeNames.IsHeading(TypographyRole.Brand);
        var takesColor = ThemeNames.TakesColor(TypographyRole.Brand);

        // Assert
        css.ShouldBe("brand");
        field.ShouldBe("brand");
        heading.ShouldBeFalse();
        takesColor.ShouldBeTrue();
        SiteThemeStylesheet.HeadingColorVariables.ShouldNotContain("--site-brand-color");
    }

    [Theory]
    [InlineData(TypographyRole.H1, true)]
    [InlineData(TypographyRole.CardTitle, true)]
    [InlineData(TypographyRole.Brand, true)]
    [InlineData(TypographyRole.Body, false)]
    [InlineData(TypographyRole.Link, false)]
    [InlineData(TypographyRole.Nav, false)]
    public void Headings_card_titles_and_the_brand_take_a_colour(TypographyRole role, bool expected)
    {
        // Arrange & Act
        var takesColor = ThemeNames.TakesColor(role);

        // Assert
        takesColor.ShouldBe(expected);
    }

    [Fact]
    public void A_theme_brand_colour_renders_the_brand_colour_variable()
    {
        // Arrange
        var brand = TypographyStyle.Create(color: ThemeColorRole.Text).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.Brand, brand)).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-brand-color: var(--site-color-text);", StringComparison.Ordinal);
    }

    [Fact]
    public void The_preview_brand_falls_back_to_the_primary_colour_like_the_site_stylesheet()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        var rule = SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__brand");
        rule.ShouldContain("color: var(--site-brand-color, var(--site-color-primary));", StringComparison.Ordinal);
        rule.ShouldNotContain("--site-brand-color, inherit", StringComparison.Ordinal);
        SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__card-title")
            .ShouldContain("color: var(--site-card-title-color, inherit);", StringComparison.Ordinal);
    }

    [Fact]
    public void A_theme_with_a_display_font_on_its_title_gives_it_to_the_brand()
    {
        // Arrange
        var h1 = TypographyStyle.Create(fontKey: "display", fallback: GenericFontFamily.Serif, weight: 600, size: SiteTheme.Default.Typography[TypographyRole.H1].Size).ShouldBeSuccess();
        var typography = SiteTheme.Default.Typography.With(TypographyRole.H1, h1);
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography, [ThemeFixtures.Font("display", "Playfair Display")]).ShouldBeSuccess();

        // Act
        var brand = theme.Effective(TypographyRole.Brand);
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        brand.FontKey.ShouldBe("display");
        brand.Weight.ShouldBe(600);
        brand.Size.ShouldBe(SiteTheme.Default.Effective(TypographyRole.Brand).Size);
        css.ShouldContain("--site-brand-font-family: \"Playfair Display\", Georgia, \"Times New Roman\", Times, serif;", StringComparison.Ordinal);
    }

    [Fact]
    public void A_brand_error_names_its_field()
    {
        // Arrange
        var tiny = ThemeFixtures.Sized(0.75m);

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.Brand, tiny));

        // Assert
        result.FieldErrors().Keys.ShouldBe(["typography.brand.size"]);
    }

    [Theory]
    [InlineData("font-family: var(--site-brand-font-family, var(--site-font-heading))")]
    [InlineData("font-size: var(--site-brand-font-size, var(--site-font-size-lg))")]
    [InlineData("font-weight: var(--site-brand-font-weight, 700)")]
    [InlineData("line-height: var(--site-brand-line-height, 1.2)")]
    [InlineData("letter-spacing: var(--site-brand-letter-spacing, normal)")]
    [InlineData("text-transform: var(--site-brand-text-transform, none)")]
    [InlineData("font-style: var(--site-brand-font-style, normal)")]
    [InlineData("color: var(--site-brand-color, var(--site-color-primary))")]
    [InlineData("text-decoration: none")]
    public void The_stylesheet_gives_a_brand_link_the_role_with_fallbacks_and_no_underline(string declaration)
    {
        // Arrange & Act
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Assert
        SiteFiles.Rule(css, ".site-header__brand a").ShouldContain(declaration, StringComparison.Ordinal);
    }

    [Fact]
    public void The_preview_brand_uses_the_brand_role()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__brand")
            .ShouldContain("font-family: var(--site-brand-font-family, inherit)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_editor_model_has_brand_fields()
    {
        // Arrange & Act
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);

        // Assert
        model.Typography[TypographyRole.Brand].SizeMin.ShouldBe(1.125m);
        model.Typography[TypographyRole.Brand].LineHeight.ShouldBe(1.2m);
    }
}
