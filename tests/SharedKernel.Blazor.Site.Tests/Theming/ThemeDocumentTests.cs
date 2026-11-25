using SharedKernel.Blazor.Site.Theming;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests the plain-data theme document: its role names, its mapping to the editor model, its version and its rules.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemeDocumentTests
{
    [Fact]
    public void Role_names_are_the_camel_case_member_names_the_stored_documents_use()
    {
        // Arrange
        var colors = Enum.GetValues<ThemeColorRole>();
        var typography = Enum.GetValues<TypographyRole>();

        // Act
        var colorNames = colors.Select(ThemeDocument.RoleName).ToArray();
        var typographyNames = typography.Select(ThemeDocument.RoleName).ToArray();

        // Assert
        colorNames.ShouldBe(["primary", "primaryHover", "onPrimary", "accent", "accentHover", "onAccent", "text", "muted", "surface", "surfaceAlt", "border", "focus"]);
        typographyNames.ShouldBe(["h1", "h2", "h3", "h4", "h5", "h6", "body", "small", "link", "button", "nav", "cardTitle", "brand"]);
    }

    [Fact]
    public void A_document_of_a_theme_leaves_out_a_derived_hover_and_unset_roles()
    {
        // Arrange
        var theme = ThemeDocumentSamples.Rich();

        // Act
        var document = ThemeDocument.From(theme);

        // Assert
        document.Version.ShouldBe(ThemeDocument.CurrentVersion);
        document.Colors.ShouldNotBeNull().Keys.ShouldNotContain("primaryHover");
        document.Colors.ShouldNotBeNull().Keys.ShouldContain("accentHover");
        document.Typography.ShouldNotBeNull().Keys.ShouldNotContain("link");
        document.Header.ShouldNotBeNull().BrandHeight.ShouldBe(3.25m);
        document.Shape.ShouldNotBeNull().Control.ShouldBe(ControlShape.Pill);
    }

    [Fact]
    public void A_document_of_empty_editor_fields_has_no_colours_typography_header_or_shape()
    {
        // Arrange
        var model = new ThemeEditorModel();

        // Act
        var document = ThemeDocument.From(model);

        // Assert
        document.Colors.ShouldNotBeNull().ShouldBeEmpty();
        document.Typography.ShouldNotBeNull().ShouldBeEmpty();
        document.Fonts.ShouldNotBeNull().ShouldBeEmpty();
        document.Header.ShouldBeNull();
        document.Shape.ShouldBeNull();
    }

    [Fact]
    public void A_document_fills_the_editor_with_every_field_it_holds()
    {
        // Arrange
        var document = ThemeDocument.From(ThemeDocumentSamples.Rich());

        // Act
        var model = document.ToEditorModel().ShouldBeSuccess();

        // Assert
        model.ToTheme().ShouldBeSuccess().ShouldBe(ThemeDocumentSamples.Rich());
        model.Colors[ThemeColorRole.PrimaryHover].Light.ShouldBeNull();
        model.Typography[TypographyRole.H2].Color.ShouldBe(ThemeColorRole.Primary);
        model.Fonts.ShouldHaveSingleItem().Faces.Count.ShouldBe(2);
        model.Fonts[0].Faces[1].Style.ShouldBe(ThemeFontStyle.Italic);
        model.ControlShape.ShouldBe(ControlShape.Pill);
        model.DividerColor.ShouldBe(ThemeColorRole.Primary);
        model.BrandHeight.ShouldBe(3.25m);
        model.AllowSmallText.ShouldBeTrue();
    }

    [Fact]
    public void Missing_entries_and_values_keep_the_editor_empty()
    {
        // Arrange
        var document = new ThemeDocument(
            new Dictionary<string, ThemeColorDocument> { ["primary"] = null! },
            new Dictionary<string, TypographyDocument> { ["h1"] = null! },
            [null!, new ThemeFontDocument("brand", "Brand", [null!])]);

        // Act
        var model = document.ToEditorModel().ShouldBeSuccess();

        // Assert
        model.Colors[ThemeColorRole.Primary].Light.ShouldBeNull();
        model.Typography[TypographyRole.H1].SizeMin.ShouldBeNull();
        model.Fonts.Count.ShouldBe(2);
        model.Fonts[0].Key.ShouldBeNull();
        model.Fonts[1].Faces.ShouldHaveSingleItem().Style.ShouldBe(ThemeFontStyle.Normal);
        model.Fonts[1].Faces[0].Src.ShouldBeNull();
    }

    [Fact]
    public void An_unknown_colour_or_typography_role_is_an_error_on_its_section()
    {
        // Arrange
        var document = new ThemeDocument(
            new Dictionary<string, ThemeColorDocument> { ["brand"] = new("#000000", "#ffffff") },
            new Dictionary<string, TypographyDocument> { ["title"] = new() });

        // Act
        var result = document.ToEditorModel();

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        var errors = result.FieldErrors();
        errors.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe([ThemeDocument.ColorsField, ThemeDocument.TypographyField]);
        errors[ThemeDocument.ColorsField].ShouldHaveSingleItem().ShouldStartWith("A color role is not known. Use primary, primaryHover,", StringComparison.Ordinal);
        errors[ThemeDocument.TypographyField].ShouldHaveSingleItem().ShouldStartWith("A typography role is not known. Use h1, h2,", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void An_unsupported_version_is_an_error_on_the_version_field(int version)
    {
        // Arrange
        var document = ThemeDocument.From(SiteTheme.Default) with { Version = version };

        // Act
        var result = document.ToTheme();

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors()[ThemeDocument.VersionField].ShouldBe([$"The theme format version {version} is not supported. Use version 1 to 1."]);
    }

    [Fact]
    public void Building_the_theme_enforces_every_rule_with_the_hosts_colour_names()
    {
        // Arrange
        var document = ThemeDocument.From(SiteTheme.Default) with
        {
            Colors = new Dictionary<string, ThemeColorDocument>(ThemeDocument.From(SiteTheme.Default).Colors!) { ["muted"] = new("#eeeeee", "#9ca3af") },
        };

        // Act
        var result = document.ToTheme(role => role == ThemeColorRole.Muted ? "Muted text" : null);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors()["colors.muted"].ShouldContain(message => message.StartsWith("Muted text on surface", StringComparison.Ordinal));
    }

    [Fact]
    public void Restoring_skips_the_readability_rules_a_stored_theme_met_when_it_was_stored()
    {
        // Arrange
        var document = ThemeDocument.From(SiteTheme.Default) with
        {
            Colors = new Dictionary<string, ThemeColorDocument>(ThemeDocument.From(SiteTheme.Default).Colors!) { ["muted"] = new("#eeeeee", "#9ca3af") },
        };

        // Act
        var theme = document.Restore();

        // Assert
        theme.Colors[ThemeColorRole.Muted].Light.Value.ShouldBe("#eeeeee");
        document.ToTheme().IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Restoring_a_document_with_a_malformed_value_fails()
    {
        // Arrange
        var document = new ThemeDocument(new Dictionary<string, ThemeColorDocument> { ["primary"] = new("blue", "#ffffff") });

        // Act
        Action restore = () => document.Restore();
        var exception = restore.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The stored site theme cannot be read.");
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        // Arrange & Act & Assert
        Action fromTheme = () => ThemeDocument.From((SiteTheme)null!);
        fromTheme.ShouldThrow<ArgumentNullException>();
        Action fromModel = () => ThemeDocument.From((ThemeEditorModel)null!);
        fromModel.ShouldThrow<ArgumentNullException>();
    }
}
