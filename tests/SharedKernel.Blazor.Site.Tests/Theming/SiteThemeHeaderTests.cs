using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests the brand height of a theme and the header height computed from it.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteThemeHeaderTests
{
    [Fact]
    public void The_default_theme_has_a_2_5rem_brand_and_a_4_5rem_header()
    {
        // Arrange & Act
        var theme = SiteTheme.Default;

        // Assert
        theme.BrandHeight.ShouldBe(SiteTheme.DefaultBrandHeight);
        SiteTheme.DefaultBrandHeight.ShouldBe(2.5m);
        theme.HeaderHeight.ShouldBe(4.5m);
    }

    [Theory]
    [InlineData("3.5", "5.5")]
    [InlineData("2", "4.3")]
    [InlineData("1.5", "4.3")]
    public void The_header_is_its_padding_and_the_taller_of_the_brand_and_the_navigation_line(string brand, string header)
    {
        // Arrange: the navigation line is the largest body size 1.125rem times its line height 1.6, plus 0.5rem of link padding, 2.3rem.
        var brandHeight = ThemeFixtures.Rem(brand);

        // Act
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: brandHeight).ShouldBeSuccess();

        // Assert
        theme.BrandHeight.ShouldBe(brandHeight);
        theme.HeaderHeight.ShouldBe(ThemeFixtures.Rem(header));
    }

    [Fact]
    public void A_navigation_style_of_its_own_sets_the_navigation_line()
    {
        // Arrange: 1.5rem at line height 2 is 3rem, plus 0.5rem of padding, 3.5rem, taller than the 2.5rem brand.
        var nav = TypographyStyle.Create(size: FontSize.Fluid(1.25m, 1.5m).ShouldBeSuccess(), lineHeight: 2m).ShouldBeSuccess();
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.Nav, nav);

        // Act
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography).ShouldBeSuccess();

        // Assert
        theme.HeaderHeight.ShouldBe(5.5m);
    }

    [Fact]
    public void Without_a_navigation_style_the_navigation_line_follows_the_body()
    {
        // Arrange: a 1.25rem body at line height 2 makes a 3rem navigation line.
        var body = TypographyStyle.Create(size: FontSize.Fixed(1.25m).ShouldBeSuccess(), lineHeight: 2m).ShouldBeSuccess();
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.Body, body);

        // Act
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography).ShouldBeSuccess();

        // Assert
        theme.HeaderHeight.ShouldBe(5m);
    }

    [Fact]
    public void The_theme_stylesheet_renders_the_brand_and_header_heights()
    {
        // Arrange
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: 3.5m).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContainInOrder("--site-scrim-boost: ", "--site-brand-height: 3.5rem;", "--site-header-height: 5.5rem;");
        SiteThemeStylesheet.HeaderVariables.ShouldBe(["--site-brand-height", "--site-header-height"]);
    }

    [Fact]
    public void The_stylesheet_defaults_are_the_heights_of_the_default_theme()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var root = SiteFiles.Rule(css, ":root");

        // Assert
        root.ShouldContain($"--site-brand-height: {SiteTheme.Default.BrandHeight}rem;", StringComparison.Ordinal);
        root.ShouldContain($"--site-header-height: {SiteTheme.Default.HeaderHeight}rem;", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-header__brand img").ShouldContain("height: var(--site-brand-height)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_computation_uses_the_paddings_the_stylesheet_gives_the_header_and_its_links()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var root = SiteFiles.Rule(css, ":root");
        var inner = System.Text.RegularExpressions.Regex.Match(css, @"^\.site-header__inner \{([^}]*)\}", System.Text.RegularExpressions.RegexOptions.Multiline, TimeSpan.FromSeconds(1)).Groups[1].Value;

        // Assert
        root.ShouldContain("--site-space-1: 0.25rem;", StringComparison.Ordinal);
        root.ShouldContain("--site-space-3: 1rem;", StringComparison.Ordinal);
        inner.ShouldContain("padding-block: var(--site-space-3)", StringComparison.Ordinal);
        SiteFiles.Rule(css, ".site-nav a").ShouldContain("padding-block: var(--site-space-1)", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.4")]
    [InlineData("8.1")]
    public void A_brand_height_outside_1_5_to_8rem_is_an_error_on_its_field(string brand)
    {
        // Arrange & Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: ThemeFixtures.Rem(brand));

        // Assert
        result.FieldErrors()["header.brandHeight"].ShouldBe(["The brand height must be from 1.5rem to 8rem."]);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("8")]
    public void A_brand_height_at_either_limit_is_allowed(string brand)
    {
        // Arrange & Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: ThemeFixtures.Rem(brand));

        // Assert
        result.ShouldBeSuccess().BrandHeight.ShouldBe(ThemeFixtures.Rem(brand));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.00001")]
    public void A_draft_needs_a_positive_brand_height_with_at_most_four_decimals(string brand)
    {
        // Arrange & Act
        var result = SiteTheme.CreateDraft(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: ThemeFixtures.Rem(brand));

        // Assert
        result.FieldErrors()["header.brandHeight"].ShouldBe(["The brand height must be a positive size in rem with at most four decimals."]);
    }

    [Fact]
    public void A_draft_keeps_a_brand_height_outside_the_rule_so_a_preview_can_show_it()
    {
        // Arrange & Act
        var draft = SiteTheme.CreateDraft(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: 9m).ShouldBeSuccess();

        // Assert
        draft.BrandHeight.ShouldBe(9m);
        draft.Review().Errors.ShouldContain(issue => issue.Field == "header.brandHeight");
    }

    [Fact]
    public void Themes_that_differ_only_in_brand_height_differ()
    {
        // Arrange & Act
        var taller = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: 3m).ShouldBeSuccess();

        // Assert
        taller.ShouldNotBe(SiteTheme.Default);
        SiteThemeStylesheet.Hash(taller).ShouldNotBe(SiteThemeStylesheet.Hash(SiteTheme.Default));
    }

    [Fact]
    public void The_editor_model_carries_the_brand_height_and_reports_it_on_its_field()
    {
        // Arrange
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: 3.5m).ShouldBeSuccess();
        var model = ThemeEditorModel.FromTheme(theme);
        var empty = ThemeEditorModel.FromTheme(SiteTheme.Default);
        empty.BrandHeight = null;
        var invalid = ThemeEditorModel.FromTheme(SiteTheme.Default);
        invalid.BrandHeight = 12m;

        // Act
        var rebuilt = model.ToTheme().ShouldBeSuccess();
        var defaulted = empty.ToTheme().ShouldBeSuccess();
        var refused = invalid.ToTheme();

        // Assert
        model.BrandHeight.ShouldBe(3.5m);
        rebuilt.ShouldBe(theme);
        defaulted.BrandHeight.ShouldBe(SiteTheme.DefaultBrandHeight);
        refused.FieldErrors().Keys.ShouldContain("header.brandHeight");
    }
}
