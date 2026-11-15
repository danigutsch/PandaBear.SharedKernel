using PandaBear.Blazor.Site.Theming;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the form model that maps editor fields to the theme value objects with field-path errors.</summary>
public sealed class ThemeEditorModelTests
{
    [Fact]
    public void A_model_filled_from_a_theme_builds_the_same_theme()
    {
        // Arrange
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, [ThemeFixtures.Font("brand", "Inter")], allowSmallText: true).ShouldBeSuccess();

        // Act
        var model = ThemeEditorModel.FromTheme(theme);
        var rebuilt = model.ToTheme().ShouldBeSuccess();

        // Assert
        rebuilt.ShouldBe(theme);
        model.Colors[ThemeColorRole.Primary].Light.ShouldBe("#1d4ed8");
        model.Typography[TypographyRole.H1].SizeMin.ShouldBe(2.125m);
        model.Typography[TypographyRole.H1].SizeMax.ShouldBe(3.5m);
        model.Fonts.ShouldHaveSingleItem().Faces.ShouldHaveSingleItem().Src.ShouldBe("/fonts/brand.woff2");
    }

    [Fact]
    public void A_new_model_has_fields_for_every_colour_and_typography_role()
    {
        // Arrange & Act
        var model = new ThemeEditorModel();

        // Assert
        model.Colors.Keys.Order().ToArray().ShouldBe(Enum.GetValues<ThemeColorRole>());
        model.Typography.Keys.Order().ToArray().ShouldBe(Enum.GetValues<TypographyRole>());
        model.Fonts.ShouldBeEmpty();
    }

    [Fact]
    public void Every_invalid_field_is_reported_at_once_with_its_path()
    {
        // Arrange
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        model.Colors[ThemeColorRole.Primary].Light = "blue";
        model.Colors[ThemeColorRole.Surface].Dark = null;
        model.Typography[TypographyRole.H2].SizeMin = null;
        model.Typography[TypographyRole.H2].SizeMax = 2m;
        model.Typography[TypographyRole.Body].Weight = 450;
        model.Typography[TypographyRole.Small].FontKey = "Not A Key";
        model.Fonts.Add(new ThemeFontFields { Key = "brand", Family = "Inter\"}", Faces = { new ThemeFontFaceFields { Weight = 400, Src = "/fonts/brand.ttf" } } });

        // Act
        var result = model.ToDraft();

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors().Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(
        [
            "colors.primary.light",
            "colors.surface.dark",
            "fonts[0].faces[0].src",
            "fonts[0].family",
            "typography.body.weight",
            "typography.h2.size",
            "typography.small.fontKey",
        ]);
    }

    [Fact]
    public void Empty_hover_fields_derive_the_hover_colour_and_one_empty_side_is_an_error()
    {
        // Arrange
        var derived = ThemeEditorModel.FromTheme(SiteTheme.Default);
        derived.Colors[ThemeColorRole.PrimaryHover].Light = null;
        derived.Colors[ThemeColorRole.PrimaryHover].Dark = " ";
        var halfEmpty = ThemeEditorModel.FromTheme(SiteTheme.Default);
        halfEmpty.Colors[ThemeColorRole.AccentHover].Dark = string.Empty;

        // Act
        var theme = derived.ToTheme().ShouldBeSuccess();
        var result = halfEmpty.ToDraft();

        // Assert
        theme.Colors.IsDerived(ThemeColorRole.PrimaryHover).ShouldBeTrue();
        ThemeEditorModel.FromTheme(theme).Colors[ThemeColorRole.PrimaryHover].Light.ShouldBeNull();
        result.FieldErrors().Keys.ShouldBe(["colors.accentHover.dark"]);
    }

    [Fact]
    public void Rule_violations_build_a_draft_but_not_a_theme()
    {
        // Arrange
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        model.Colors[ThemeColorRole.Muted].Light = "#d1d5db";

        // Act
        var draft = model.ToDraft();
        var theme = model.ToTheme();

        // Assert
        draft.ShouldBeSuccess();
        theme.FieldErrors().Keys.ShouldBe(["colors.muted"]);
    }

    [Fact]
    public void Rule_errors_of_the_theme_name_the_colours_by_the_hosts_labels()
    {
        // Arrange
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        model.Colors[ThemeColorRole.Muted].Light = "#d1d5db";

        // Act
        var theme = model.ToTheme(ThemeFixtures.EditorLabel);

        // Assert
        theme.FieldErrors()["colors.muted"].ShouldAllSatisfy(message => message.ShouldStartWith("Muted text on ", StringComparison.Ordinal));
        theme.FieldErrors()["colors.muted"].Length.ShouldBe(2);
    }

    [Fact]
    public void Font_reference_errors_from_the_theme_keep_their_typography_path()
    {
        // Arrange
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        model.Typography[TypographyRole.H1].FontKey = "display";

        // Act
        var result = model.ToDraft();

        // Assert
        result.FieldErrors().Keys.ShouldBe(["typography.h1.fontKey"]);
    }

    [Fact]
    public void A_fixed_size_is_a_minimum_without_a_maximum_or_with_an_equal_one()
    {
        // Arrange
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        model.Typography[TypographyRole.Button].SizeMin = 1.125m;
        model.Typography[TypographyRole.Nav].SizeMin = 1m;
        model.Typography[TypographyRole.Nav].SizeMax = 1m;

        // Act
        var theme = model.ToTheme().ShouldBeSuccess();

        // Assert
        theme.Typography[TypographyRole.Button].Size!.IsFluid.ShouldBeFalse();
        theme.Typography[TypographyRole.Button].Size!.Min.ShouldBe(1.125m);
        theme.Typography[TypographyRole.Nav].Size!.IsFluid.ShouldBeFalse();
    }
}
