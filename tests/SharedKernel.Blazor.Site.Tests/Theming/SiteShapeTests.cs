using SharedKernel.Blazor.Site.Theming;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests the shape of a theme: the control shape and the divider, their rules and their custom properties.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteShapeTests
{
    [Fact]
    public void The_default_shape_is_rounded_with_a_4rem_accent_divider_a_quarter_rem_thick()
    {
        // Arrange & Act
        var shape = SiteTheme.Default.Shape;

        // Assert
        shape.ShouldBe(SiteShape.Default);
        shape.Control.ShouldBe(ControlShape.Rounded);
        shape.DividerWidth.ShouldBe(4m);
        shape.DividerThickness.ShouldBe(0.25m);
        shape.DividerColor.ShouldBe(ThemeColorRole.Accent);
    }

    [Theory]
    [InlineData(ControlShape.Square, "0", "0")]
    [InlineData(ControlShape.Rounded, "0.5rem", "0.5rem")]
    [InlineData(ControlShape.Pill, "0.5rem", "999rem")]
    public void The_control_shape_sets_the_surface_and_the_control_radius(ControlShape control, string surface, string controlRadius)
    {
        // Arrange
        var shape = SiteShape.Create(control).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContainInOrder(
            "--site-header-height: ",
            $"--site-radius: {surface};",
            $"--site-control-radius: {controlRadius};",
            "--site-divider-width: 4rem;",
            "--site-divider-thickness: 0.25rem;",
            "--site-divider-color: var(--site-color-accent);");
        SiteThemeStylesheet.ShapeVariables.ShouldBe(["--site-radius", "--site-control-radius", "--site-divider-width", "--site-divider-thickness", "--site-divider-color"]);
    }

    [Fact]
    public void A_divider_of_its_own_renders_its_width_thickness_and_colour()
    {
        // Arrange
        var shape = SiteShape.Create(ControlShape.Pill, dividerWidth: 4.375m, dividerThickness: 0.1875m, dividerColor: ThemeColorRole.Primary).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape).ShouldBeSuccess();

        // Act
        var css = SiteThemeStylesheet.Render(theme);

        // Assert
        css.ShouldContain("--site-divider-width: 4.375rem;", StringComparison.Ordinal);
        css.ShouldContain("--site-divider-thickness: 0.1875rem;", StringComparison.Ordinal);
        css.ShouldContain("--site-divider-color: var(--site-color-primary);", StringComparison.Ordinal);
    }

    [Fact]
    public void The_stylesheet_defaults_are_the_shape_of_the_default_theme()
    {
        // Arrange
        var theme = SiteThemeStylesheet.Render(SiteTheme.Default);
        var root = SiteFiles.Rule(File.ReadAllText(SiteFiles.StylesheetPath()), ":root");

        // Act
        var declarations = SiteThemeStylesheet.ShapeVariables
            .Select(variable => theme.Split('\n').Where(line => line.TrimStart().StartsWith(variable + ":", StringComparison.Ordinal)).ShouldHaveSingleItem().Trim())
            .ToArray();

        // Assert
        declarations.ShouldAllSatisfy(declaration => root.ShouldContain(declaration, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0", "0.25", "dividerWidth")]
    [InlineData("-1", "0.25", "dividerWidth")]
    [InlineData("4.00001", "0.25", "dividerWidth")]
    [InlineData("4", "0", "dividerThickness")]
    [InlineData("4", "0.123456", "dividerThickness")]
    public void A_shape_needs_a_positive_divider_with_at_most_four_decimals(string width, string thickness, string field)
    {
        // Arrange & Act
        var result = SiteShape.Create(dividerWidth: ThemeFixtures.Rem(width), dividerThickness: ThemeFixtures.Rem(thickness));

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors().Keys.ShouldBe([field]);
    }

    [Fact]
    public void An_undefined_control_shape_is_an_error_on_its_field()
    {
        // Arrange & Act
        var result = SiteShape.Create((ControlShape)9);

        // Assert
        result.FieldErrors()["control"].ShouldBe(["The control shape must be square, rounded or pill."]);
    }

    [Theory]
    [InlineData(ThemeColorRole.OnPrimary)]
    [InlineData(ThemeColorRole.Surface)]
    [InlineData(ThemeColorRole.Focus)]
    [InlineData((ThemeColorRole)40)]
    public void A_divider_takes_a_colour_drawn_on_the_page_only(ThemeColorRole color)
    {
        // Arrange & Act
        var result = SiteShape.Create(dividerColor: color);

        // Assert
        result.FieldErrors()["dividerColor"].ShouldBe(["The divider colour must be the primary, accent, text, muted or border colour."]);
    }

    [Theory]
    [InlineData("0.9", "0.25", "shape.dividerWidth", "The divider width must be from 1rem to 16rem.")]
    [InlineData("16.5", "0.25", "shape.dividerWidth", "The divider width must be from 1rem to 16rem.")]
    [InlineData("4", "0.05", "shape.dividerThickness", "The divider thickness must be from 0.0625rem to 1rem.")]
    [InlineData("4", "1.25", "shape.dividerThickness", "The divider thickness must be from 0.0625rem to 1rem.")]
    public void A_divider_outside_the_layout_range_breaks_a_rule(string width, string thickness, string field, string message)
    {
        // Arrange
        var shape = SiteShape.Create(dividerWidth: ThemeFixtures.Rem(width), dividerThickness: ThemeFixtures.Rem(thickness)).ShouldBeSuccess();

        // Act
        var created = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape);
        var draft = SiteTheme.CreateDraft(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape).ShouldBeSuccess();

        // Assert
        created.FieldErrors()[field].ShouldBe([message]);
        draft.Shape.ShouldBe(shape);
        draft.Review().Errors.ShouldContain(issue => issue.Field == field);
    }

    [Theory]
    [InlineData("1", "0.0625")]
    [InlineData("16", "1")]
    public void A_divider_at_either_limit_is_allowed(string width, string thickness)
    {
        // Arrange
        var shape = SiteShape.Create(dividerWidth: ThemeFixtures.Rem(width), dividerThickness: ThemeFixtures.Rem(thickness)).ShouldBeSuccess();

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape);

        // Assert
        result.ShouldBeSuccess().Shape.ShouldBe(shape);
    }

    [Fact]
    public void Themes_that_differ_only_in_shape_differ()
    {
        // Arrange & Act
        var pill = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: SiteShape.Create(ControlShape.Pill).ShouldBeSuccess()).ShouldBeSuccess();

        // Assert
        pill.ShouldNotBe(SiteTheme.Default);
        SiteThemeStylesheet.Hash(pill).ShouldNotBe(SiteThemeStylesheet.Hash(SiteTheme.Default));
    }

    [Fact]
    public void The_editor_model_carries_the_shape_and_reports_it_on_its_fields()
    {
        // Arrange
        var shape = SiteShape.Create(ControlShape.Pill, 4.375m, 0.1875m, ThemeColorRole.Primary).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, shape: shape).ShouldBeSuccess();
        var model = ThemeEditorModel.FromTheme(theme);
        var empty = ThemeEditorModel.FromTheme(SiteTheme.Default);
        empty.ControlShape = null;
        empty.DividerWidth = null;
        empty.DividerThickness = null;
        empty.DividerColor = null;
        var invalid = ThemeEditorModel.FromTheme(SiteTheme.Default);
        invalid.DividerColor = ThemeColorRole.OnAccent;
        invalid.DividerWidth = 30m;

        // Act
        var rebuilt = model.ToTheme().ShouldBeSuccess();
        var defaulted = empty.ToTheme().ShouldBeSuccess();
        var refused = invalid.ToTheme();

        // Assert
        model.ControlShape.ShouldBe(ControlShape.Pill);
        model.DividerWidth.ShouldBe(4.375m);
        model.DividerThickness.ShouldBe(0.1875m);
        model.DividerColor.ShouldBe(ThemeColorRole.Primary);
        rebuilt.ShouldBe(theme);
        defaulted.Shape.ShouldBe(SiteShape.Default);
        refused.FieldErrors().Keys.ShouldContain("shape.dividerColor");
    }

    [Fact]
    public void The_preview_draws_buttons_and_cards_with_the_shape_and_the_button_padding_of_the_site()
    {
        // Arrange
        var site = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var css = SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "preview-1");
        var button = SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-button");
        var card = SiteFiles.Rule(css, "#preview-1 .site-theme-preview__sample .site-card");

        // Assert
        button.ShouldContain("border-radius: var(--site-control-radius)", StringComparison.Ordinal);
        button.ShouldContain("padding: " + SiteThemeStylesheet.ButtonPadding, StringComparison.Ordinal);
        button.ShouldNotContain("0.5rem 1.5rem", StringComparison.Ordinal);
        card.ShouldContain("border-radius: var(--site-radius)", StringComparison.Ordinal);
        SiteFiles.Rule(site, ".site-button").ShouldContain("padding: " + SiteThemeStylesheet.ButtonPadding, StringComparison.Ordinal);
        css.ShouldContain("--site-control-radius: 0.5rem;", StringComparison.Ordinal);
    }
}
