using System.Text.Json;
using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>
/// Tests the stored JSON of a theme against text written by the first consumer before the format was versioned, so every stored theme
/// still loads and a new save differs from it only by the version.
/// </summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemeDocumentJsonTests
{
    [Fact]
    public void Unversioned_text_of_the_default_theme_restores_the_default_theme()
    {
        // Arrange
        var text = ThemeDocumentSamples.UnversionedDefaultText;

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.ShouldBe(SiteTheme.Default);
    }

    [Fact]
    public void Unversioned_text_of_a_theme_with_every_kind_of_value_restores_the_same_theme()
    {
        // Arrange
        var text = ThemeDocumentSamples.UnversionedRichText;

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.ShouldBe(ThemeDocumentSamples.Rich());
        SiteThemeStylesheet.Render(theme).ShouldBe(SiteThemeStylesheet.Render(ThemeDocumentSamples.Rich()));
    }

    [Fact]
    public void Serialize_writes_the_unversioned_text_with_the_version_first()
    {
        // Arrange
        var themes = new[] { SiteTheme.Default, ThemeDocumentSamples.Rich() };
        var unversioned = new[] { ThemeDocumentSamples.UnversionedDefaultText, ThemeDocumentSamples.UnversionedRichText };

        // Act
        var written = themes.Select(ThemeDocumentJson.Serialize).ToArray();

        // Assert
        written.ShouldBe(unversioned.Select(text => "{\"version\":1," + text[1..]).ToArray());
    }

    [Fact]
    public void Written_text_restores_the_same_theme()
    {
        // Arrange
        var theme = ThemeDocumentSamples.Rich();

        // Act
        var restored = ThemeDocumentJson.Restore(ThemeDocumentJson.Serialize(theme));

        // Assert
        restored.ShouldBe(theme);
    }

    [Fact]
    public void Text_of_the_first_shape_without_header_shape_or_heading_colours_restores_with_their_defaults()
    {
        // Arrange
        var text = ThemeDocumentSamples.FirstShapeText;

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.BrandHeight.ShouldBe(SiteTheme.DefaultBrandHeight);
        theme.Shape.ShouldBe(SiteShape.Default);
        theme.Typography[TypographyRole.H2].Color.ShouldBeNull();
        theme.Colors[ThemeColorRole.Primary].Light.Value.ShouldBe("#1f4399");
        theme.Typography[TypographyRole.H2].Transform.ShouldBe(ThemeTextTransform.Uppercase);
        theme.Fonts.ShouldHaveSingleItem().Key.ShouldBe("noto-sans");
    }

    [Fact]
    public void Text_with_a_header_but_no_shape_restores_its_logo_height_and_the_default_shape()
    {
        // Arrange
        var text = ThemeDocumentSamples.HeaderShapeText;

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.BrandHeight.ShouldBe(3.25m);
        theme.Shape.ShouldBe(SiteShape.Default);
    }

    [Fact]
    public void Text_of_version_one_restores_like_unversioned_text()
    {
        // Arrange
        var text = "{\"version\":1," + ThemeDocumentSamples.UnversionedRichText[1..];

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.ShouldBe(ThemeDocumentSamples.Rich());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Text_of_an_unsupported_version_is_refused_naming_the_version(int version)
    {
        // Arrange
        var text = "{\"version\":" + version + "," + ThemeDocumentSamples.UnversionedDefaultText[1..];

        // Act
        Action restore = () => ThemeDocumentJson.Restore(text);
        var exception = restore.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe($"The stored site theme has format version {version}; this library reads version 1 to 1.");
    }

    [Fact]
    public void An_enum_written_as_a_number_is_refused()
    {
        // Arrange
        const string text = "{\"shape\":{\"control\":2}}";

        // Act & Assert
        Action restore = () => ThemeDocumentJson.Restore(text);
        restore.ShouldThrow<JsonException>();
    }

    [Fact]
    public void A_member_the_format_does_not_define_is_ignored()
    {
        // Arrange
        var text = "{\"comment\":\"kept by an older tool\"," + ThemeDocumentSamples.UnversionedDefaultText[1..];

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.ShouldBe(SiteTheme.Default);
    }

    [Fact]
    public void Members_are_read_whatever_their_case()
    {
        // Arrange
        var text = ThemeDocumentSamples.UnversionedDefaultText.Replace("\"header\":{\"brandHeight\":2.5}", "\"Header\":{\"BrandHeight\":3}", StringComparison.Ordinal);

        // Act
        var theme = ThemeDocumentJson.Restore(text);

        // Assert
        theme.BrandHeight.ShouldBe(3m);
    }

    [Fact]
    public void The_JSON_null_is_refused_as_empty()
    {
        // Arrange
        const string text = "null";

        // Act
        Action restore = () => ThemeDocumentJson.Restore(text);
        var exception = restore.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The stored site theme is empty.");
    }

    [Fact]
    public void Text_that_names_an_unknown_role_cannot_be_read()
    {
        // Arrange
        const string text = "{\"colors\":{\"brand\":{\"light\":\"#000000\",\"dark\":\"#ffffff\"}}}";

        // Act
        Action restore = () => ThemeDocumentJson.Restore(text);
        var exception = restore.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The stored site theme cannot be read.");
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        // Arrange & Act & Assert
        Action restore = () => ThemeDocumentJson.Restore(null!);
        restore.ShouldThrow<ArgumentNullException>();
        Action serialize = () => ThemeDocumentJson.Serialize(null!);
        serialize.ShouldThrow<ArgumentNullException>();
    }
}
