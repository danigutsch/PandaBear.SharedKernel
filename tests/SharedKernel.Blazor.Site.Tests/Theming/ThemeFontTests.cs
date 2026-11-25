using SharedKernel.Blazor.Site.Theming;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests self-hosted font families and their faces.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemeFontTests
{
    [Theory]
    [InlineData("/fonts/inter-400.woff2")]
    [InlineData("/fonts/Inter.WOFF2")]
    [InlineData("/fonts/inter.woff2?v=3")]
    [InlineData("https://cdn.example.org/fonts/inter.woff2")]
    public void A_face_accepts_a_root_relative_or_https_woff2_file(string src)
    {
        // Arrange & Act
        var face = ThemeFontFace.Create(400, ThemeFontStyle.Normal, LinkTarget.FromString(src)).ShouldBeSuccess();

        // Assert
        face.Src.Value.ShouldBe(src);
        face.Weight.ShouldBe(400);
        face.Style.ShouldBe(ThemeFontStyle.Normal);
    }

    [Theory]
    [InlineData("/fonts/inter.woff")]
    [InlineData("/fonts/inter.ttf")]
    [InlineData("/fonts/inter.woff2#x")]
    [InlineData("http://cdn.example.org/inter.woff2")]
    [InlineData("fonts/inter.woff2")]
    [InlineData("/fonts/a'b.woff2")]
    [InlineData("/fonts/a(b).woff2")]
    [InlineData("/fonts/x\");}*{color:red}/*.woff2")]
    [InlineData("/fonts/x<b.woff2")]
    [InlineData("mailto:a@example.org")]
    public void A_face_refuses_other_files_schemes_and_CSS_breaking_characters(string src)
    {
        // Arrange & Act
        var result = ThemeFontFace.Create(400, ThemeFontStyle.Normal, LinkTarget.FromString(src));

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors().Keys.ShouldContain("src");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(350)]
    [InlineData(1000)]
    public void A_face_weight_must_be_a_multiple_of_100_from_100_to_900(int weight)
    {
        // Arrange & Act
        var result = ThemeFontFace.Create(weight, ThemeFontStyle.Normal, LinkTarget.FromString("/f.woff2"));

        // Assert
        result.FieldErrors().Keys.ShouldContain("weight");
    }

    [Theory]
    [InlineData("Inter")]
    [InlineData("Source Sans 3")]
    [InlineData("Noto_Sans-JP")]
    [InlineData("Montserrat Alternates")]
    [InlineData("Fira Código")]
    public void Font_family_names_of_letters_digits_spaces_hyphens_and_underscores_are_accepted(string family)
    {
        // Arrange & Act
        var font = ThemeFont.Create("brand", family, [ThemeFixtures.Face(400)]).ShouldBeSuccess();

        // Assert
        font.Family.ShouldBe(family);
        font.Key.ShouldBe("brand");
    }

    [Theory]
    [InlineData("Inter\"; } body { display: none } /*")]
    [InlineData("Inter\\")]
    [InlineData("Inter</style><script>")]
    [InlineData("Inter;")]
    [InlineData("Inter'")]
    [InlineData("Inter{")]
    [InlineData(" Inter")]
    [InlineData("Inter  Sans")]
    [InlineData("Inter\n")]
    [InlineData("")]
    [InlineData(null)]
    public void Font_family_names_that_could_break_out_of_a_CSS_string_are_invalid(string? family)
    {
        // Arrange & Act
        var result = ThemeFont.Create("brand", family, [ThemeFixtures.Face(400)]);

        // Assert
        result.FieldErrors().Keys.ShouldContain("family");
    }

    [Fact]
    public void A_family_name_longer_than_the_maximum_is_invalid()
    {
        // Arrange & Act
        var result = ThemeFont.Create("brand", new string('a', ThemeFont.MaxFamilyLength + 1), [ThemeFixtures.Face(400)]);

        // Assert
        result.FieldErrors().Keys.ShouldContain("family");
    }

    [Theory]
    [InlineData("Brand")]
    [InlineData("1brand")]
    [InlineData("br and")]
    [InlineData("")]
    public void Font_keys_must_be_lower_case_slugs(string key)
    {
        // Arrange & Act
        var result = ThemeFont.Create(key, "Inter", [ThemeFixtures.Face(400)]);

        // Assert
        result.FieldErrors().Keys.ShouldContain("key");
    }

    [Fact]
    public void A_font_needs_at_least_one_face_and_no_repeated_weight_and_style()
    {
        // Arrange & Act
        var none = ThemeFont.Create("brand", "Inter", []);
        var repeated = ThemeFont.Create("brand", "Inter", [ThemeFixtures.Face(400), ThemeFixtures.Face(400)]);

        // Assert
        none.FieldErrors().Keys.ShouldContain("faces");
        repeated.FieldErrors().Keys.ShouldContain("faces");
    }

    [Fact]
    public void Faces_are_kept_in_weight_then_style_order()
    {
        // Arrange
        var italic = ThemeFontFace.Create(400, ThemeFontStyle.Italic, LinkTarget.FromString("/i.woff2")).ShouldBeSuccess();

        // Act
        var font = ThemeFont.Create("brand", "Inter", [ThemeFixtures.Face(700), italic, ThemeFixtures.Face(400)]).ShouldBeSuccess();

        // Assert
        font.Faces.Select(face => (face.Weight, face.Style)).ShouldBe([(400, ThemeFontStyle.Normal), (400, ThemeFontStyle.Italic), (700, ThemeFontStyle.Normal)]);
    }

    [Theory]
    [InlineData("Inter", "inter")]
    [InlineData("Noto Sans", "noto-sans")]
    [InlineData("noto_sans", "noto-sans")]
    [InlineData("  Source  Sans -_ 3 ", "source-sans-3")]
    [InlineData("-Inter_", "inter")]
    [InlineData("Fira Código", "fira-código")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void The_key_of_a_family_is_lower_case_with_each_run_of_spaces_hyphens_and_underscores_as_one_hyphen(string? family, string key)
    {
        // Arrange & Act
        var derived = ThemeFont.KeyOf(family);

        // Assert
        derived.ShouldBe(key);
    }

    [Fact]
    public void A_font_for_a_family_takes_the_trimmed_family_and_its_derived_key()
    {
        // Arrange
        var italic = ThemeFontFace.Create(400, ThemeFontStyle.Italic, LinkTarget.FromString("/i.woff2")).ShouldBeSuccess();

        // Act
        var font = ThemeFont.ForFamily("  Noto Sans ", [ThemeFixtures.Face(700), italic]).ShouldBeSuccess();

        // Assert
        (font.Key, font.Family).ShouldBe(("noto-sans", "Noto Sans"));
        font.Faces.Select(face => (face.Weight, face.Style)).ShouldBe([(400, ThemeFontStyle.Italic), (700, ThemeFontStyle.Normal)]);
    }

    [Theory]
    [InlineData("9 Sans")]
    [InlineData("A family name that is much longer than thirty-two")]
    [InlineData("Fira Código")]
    public void A_valid_family_that_gives_no_key_is_reported_on_the_family_with_the_key_rule_in_its_words(string family)
    {
        // Arrange & Act
        var result = ThemeFont.ForFamily(family, [ThemeFixtures.Face(400)]);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        var errors = result.FieldErrors();
        errors.Keys.ShouldBe(["family"]);
        errors["family"].ShouldBe([ThemeFont.FamilyKeyMessage]);
    }

    [Theory]
    [InlineData("Inter\"; } body { display: none } /*")]
    [InlineData("Inter  Sans")]
    [InlineData("")]
    [InlineData(null)]
    public void An_invalid_family_is_reported_once_on_the_family_without_the_key_rule(string? family)
    {
        // Arrange & Act
        var result = ThemeFont.ForFamily(family, [ThemeFixtures.Face(400)]);

        // Assert
        var errors = result.FieldErrors();
        errors.Keys.ShouldBe(["family"]);
        errors["family"].Length.ShouldBe(1);
        errors["family"].ShouldNotContain(ThemeFont.FamilyKeyMessage);
    }

    [Fact]
    public void A_font_for_a_family_keeps_the_face_rules()
    {
        // Arrange & Act
        var none = ThemeFont.ForFamily("Inter", []);
        var repeated = ThemeFont.ForFamily("Inter", [ThemeFixtures.Face(400), ThemeFixtures.Face(400)]);

        // Assert
        none.FieldErrors().Keys.ShouldBe(["faces"]);
        repeated.FieldErrors().Keys.ShouldBe(["faces"]);
    }
}
