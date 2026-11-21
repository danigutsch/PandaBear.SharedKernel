using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the generated theme stylesheet: font faces, colour and per-role custom properties, determinism and escaping.</summary>
public sealed class SiteThemeStylesheetTests
{
    [Fact]
    public void Every_colour_token_is_rendered_with_light_dark_on_root()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Assert
        css.ShouldContain(":root {", StringComparison.Ordinal);
        css.ShouldContain("--site-color-primary: light-dark(#1d4ed8, #93c5fd);", StringComparison.Ordinal);
        css.ShouldContain("--site-color-primary-hover: light-dark(#1e40af, #60a5fa);", StringComparison.Ordinal);
        css.ShouldContain("--site-color-surface-alt: light-dark(#f3f4f6, #1f2937);", StringComparison.Ordinal);
        css.ShouldContain("--site-color-focus: light-dark(#1d4ed8, #93c5fd);", StringComparison.Ordinal);
        SiteThemeStylesheet.ColorVariables.ShouldAllSatisfy(variable => css.ShouldContain(variable + ": light-dark(", StringComparison.Ordinal));
        SiteThemeStylesheet.ColorVariables.Count.ShouldBe(12);
    }

    [Fact]
    public void Default_typography_renders_the_stylesheet_scale_as_per_role_properties()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Assert
        css.ShouldContain("--site-h1-font-size: clamp(2.125rem, 1.6667rem + 2.2917vw, 3.5rem);", StringComparison.Ordinal);
        css.ShouldContain("--site-h1-font-weight: 700;", StringComparison.Ordinal);
        css.ShouldContain("--site-h1-line-height: 1.2;", StringComparison.Ordinal);
        css.ShouldContain("--site-body-line-height: 1.6;", StringComparison.Ordinal);
        css.ShouldContain("--site-body-font-family: system-ui, -apple-system, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif;", StringComparison.Ordinal);
        css.ShouldContain("--site-button-font-weight: 600;", StringComparison.Ordinal);
        css.ShouldNotContain("--site-link-", StringComparison.Ordinal);
        css.ShouldNotContain("@font-face", StringComparison.Ordinal);
    }

    [Fact]
    public void A_complete_role_renders_all_seven_properties()
    {
        // Arrange
        var style = TypographyStyle.Create("brand", GenericFontFamily.Serif, FontSize.Fixed(1.25m).ShouldBeSuccess(), 500, 1.4m, 0.05m, ThemeTextTransform.Uppercase, true).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.Nav, style), [ThemeFixtures.Font("brand", "Playfair Display")]).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContainInOrder(
            "--site-nav-font-family: \"Playfair Display\", Georgia, \"Times New Roman\", Times, serif;",
            "--site-nav-font-size: 1.25rem;",
            "--site-nav-font-weight: 500;",
            "--site-nav-line-height: 1.4;",
            "--site-nav-letter-spacing: 0.05em;",
            "--site-nav-text-transform: uppercase;",
            "--site-nav-font-style: italic;");
        SiteThemeStylesheet.RoleVariables.Count.ShouldBe(11 * 7);
        SiteThemeStylesheet.RoleVariables.ShouldContain("--site-nav-font-style");
    }

    [Fact]
    public void Fonts_render_as_font_faces_with_swap_in_key_then_weight_order()
    {
        // Arrange
        var lora = ThemeFont.Create(
            "body",
            "Lora",
            [
                ThemeFontFace.Create(700, ThemeFontStyle.Normal, LinkTarget.FromString("/fonts/lora-700.woff2")).ShouldBeSuccess(),
                ThemeFontFace.Create(400, ThemeFontStyle.Italic, LinkTarget.FromString("https://cdn.example.org/lora-400i.woff2")).ShouldBeSuccess(),
            ]).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, [ThemeFixtures.Font("display", "Inter"), lora]).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContainInOrder(
            "@font-face {",
            "font-family: \"Lora\";",
            "src: url(\"https://cdn.example.org/lora-400i.woff2\") format(\"woff2\");",
            "font-weight: 400;",
            "font-style: italic;",
            "font-display: swap;",
            "src: url(\"/fonts/lora-700.woff2\") format(\"woff2\");",
            "font-family: \"Inter\";",
            "src: url(\"/fonts/display.woff2\") format(\"woff2\");",
            ":root {");
        theme.Fonts.Select(font => font.Key).ShouldBe(["body", "display"]);
    }

    [Fact]
    public void A_fluid_size_with_a_negative_intercept_subtracts_it()
    {
        // Arrange
        var theme = SiteTheme.CreateDraft(SiteTheme.Default.Colors, ThemeFixtures.DefaultTypographyWith(TypographyRole.H1, ThemeFixtures.Sized(1m, 5m))).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-h1-font-size: clamp(1rem, 6.6667vw - 0.3333rem, 5rem);", StringComparison.Ordinal);
    }

    [Fact]
    public void Output_and_hash_are_deterministic_and_change_with_the_theme()
    {
        // Arrange
        var changed = SiteTheme.Create(ThemeFixtures.DefaultColorsWith(ThemeColorRole.Primary, ThemeFixtures.Pair("#1e3a8a", "#93c5fd")), SiteTheme.Default.Typography).ShouldBeSuccess();
        var sameAsDefault = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        var first = SiteThemeStylesheet.Hash(SiteTheme.Default);
        var second = SiteThemeStylesheet.Hash(sameAsDefault);
        var other = SiteThemeStylesheet.Hash(changed);

        // Assert
        SiteThemeStylesheet.Render(SiteTheme.Default).ShouldBe(SiteThemeStylesheet.Render(sameAsDefault));
        first.ShouldBe(second);
        first.ShouldNotBe(other);
        first.ShouldMatch("^[0-9a-f]{16}$");
    }

    [Fact]
    public void The_hash_is_the_first_eight_bytes_of_the_sha256_of_the_utf8_stylesheet()
    {
        // Arrange
        var css = SiteThemeStylesheet.Render(SiteTheme.Default);

        // Act
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(css)))[..16];

        // Assert
        SiteThemeStylesheet.Hash(SiteTheme.Default).ShouldBe(expected);
    }

    [Fact]
    public void The_versioned_render_returns_the_existing_CSS_and_hash_together()
    {
        // Arrange
        var theme = SiteTheme.Default;
        var expectedCss = SiteThemeStylesheet.Render(theme);
        var expectedVersion = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expectedCss)))[..16];

        // Act
        var (css, version) = SiteThemeStylesheet.RenderVersioned(theme);

        // Assert
        css.ShouldBe(expectedCss);
        version.ShouldBe(expectedVersion);
    }

    [Fact]
    public void Output_contains_no_markup_and_no_comment_or_rule_breakers_from_values()
    {
        // Arrange
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, [ThemeFixtures.Font("brand", "Fira Código")]).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldNotContain("<", StringComparison.Ordinal);
        css.ShouldNotContain("\\", StringComparison.Ordinal);
        css.ShouldNotContain("/*", StringComparison.Ordinal);
        css.ShouldNotContain("@import", StringComparison.Ordinal);
        css.ShouldContain("font-family: \"Fira Código\";", StringComparison.Ordinal);
    }

    [Fact]
    public void Scoped_output_replaces_root_with_the_preview_ID_and_uses_container_units()
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        css.ShouldNotContain(":root", StringComparison.Ordinal);
        css.ShouldContain("#preview-1 {", StringComparison.Ordinal);
        css.ShouldContain("#preview-1[data-theme=\"dark\"] {", StringComparison.Ordinal);
        css.ShouldContain("color-scheme: dark;", StringComparison.Ordinal);
        css.ShouldContain("container-type: inline-size;", StringComparison.Ordinal);
        css.ShouldContain("--site-h1-font-size: clamp(2.125rem, 1.6667rem + 2.2917cqi, 3.5rem);", StringComparison.Ordinal);
        css.ShouldNotContain("vw", StringComparison.Ordinal);
        SiteThemeStylesheet.RoleVariables.ShouldAllSatisfy(variable => css.ShouldContain("var(" + variable, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("h1")]
    [InlineData("h2")]
    [InlineData("h3")]
    [InlineData("h6")]
    public void Scoped_output_styles_the_sample_headings_by_class_not_by_heading_element(string level)
    {
        // Arrange & Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");

        // Assert
        var rule = SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-theme-preview__" + level);
        rule.ShouldContain($"font-size: var(--site-{level}-font-size, inherit)", StringComparison.Ordinal);
        rule.ShouldContain("margin: 0 0 0.5rem", StringComparison.Ordinal);
        css.ShouldNotContain("#preview-1 .site-theme-preview__sample " + level + " {", StringComparison.Ordinal);
        css.ShouldNotContain(":is(h1", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("a{}")]
    [InlineData("a\"")]
    public void Scoped_output_refuses_an_unsafe_scope_ID(string scopeId)
    {
        // Arrange & Act
        var render = () => SiteThemeStylesheet.RenderScoped(SiteTheme.Default, scopeId);

        // Assert
        render.ShouldThrow<ArgumentException>();
    }
}
