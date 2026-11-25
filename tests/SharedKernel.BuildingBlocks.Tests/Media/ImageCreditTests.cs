using System.Globalization;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the attribution of a third-party image: author, title, source, licence and whether it was changed.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class ImageCreditTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly ImageCreditFormats Portuguese = new(
        WithTitle: "“{2}”, de {0}, licença {1}",
        WithoutTitle: "Foto de {0}, licença {1}",
        Modified: "{0} (adaptada)");

    [Fact]
    public void A_credit_keeps_its_sanitized_parts()
    {
        // Act
        var credit = ImageCredit.Create(
            "  Ana   Silva ", " Castelo de Neuschwanstein ", "https://www.flickr.com/photos/ana/123", "CC BY 2.0", "https://creativecommons.org/licenses/by/2.0/", modified: true)
            .ShouldBeSuccess();

        // Assert
        credit.Author.ShouldBe("Ana Silva");
        credit.Title.ShouldBe("Castelo de Neuschwanstein");
        credit.SourceUrl.ShouldBe(WebUrl.Restore("https://www.flickr.com/photos/ana/123"));
        credit.License.Code.ShouldBe("CC BY 2.0");
        credit.License.Url.ShouldBe(WebUrl.Restore("https://creativecommons.org/licenses/by/2.0/"));
        credit.Modified.ShouldBeTrue();
    }

    [Fact]
    public void Only_the_author_and_the_license_are_required()
    {
        // Act
        var credit = ImageCredit.Create("Pixabay user", null, " ", "Pixabay", null, modified: false).ShouldBeSuccess();

        // Assert
        credit.Title.ShouldBeNull();
        credit.SourceUrl.ShouldBeNull();
        credit.License.Url.ShouldBeNull();
        credit.Modified.ShouldBeFalse();
    }

    [Fact]
    public void Each_broken_part_is_refused_on_its_field()
    {
        // Act
        var missing = ImageCredit.Create(" ", null, null, null, null, false);
        var tooLong = ImageCredit.Create(new string('a', 201), new string('t', 201), "http://example.org/photo", new string('c', 61), "http://creativecommons.org", false);
        var malformed = ImageCredit.Create("Broken \uD800", "Broken \uDC00", null, "CC0 1.0", null, false);

        // Assert
        missing.ErrorDetails.ShouldHaveValidationError("author", "Author is required.");
        missing.ErrorDetails.ShouldHaveValidationError("license.code", "License is required.");
        tooLong.ErrorDetails.ShouldHaveValidationError("author", "Author cannot exceed 200 characters.");
        tooLong.ErrorDetails.ShouldHaveValidationError("title", "Title cannot exceed 200 characters.");
        tooLong.ErrorDetails.ShouldHaveValidationError("sourceUrl", "Source link must be an absolute https address.");
        tooLong.ErrorDetails.ShouldHaveValidationError("license.code", "License cannot exceed 60 characters.");
        tooLong.ErrorDetails.ShouldHaveValidationError("license.url", "License link must be an absolute https address.");
        malformed.ErrorDetails.ShouldHaveValidationError("author", "Author is not valid text.");
        malformed.ErrorDetails.ShouldHaveValidationError("title", "Title is not valid text.");
    }

    [Fact]
    public void A_field_prefix_places_the_errors_under_the_credit()
    {
        // Act
        var result = ImageCredit.Create(null, null, null, null, null, false, fieldPrefix: "credit");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys.Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(["credit.author", "credit.license.code"]);
    }

    [Fact]
    public void The_short_text_fills_the_application_format_with_the_author_license_title_and_source()
    {
        // Arrange
        var credit = ImageCredit.Create("Ana Silva", "Castelo", "https://example.org/p/1", "CC BY-SA 4.0", null, false).ShouldBeSuccess();

        // Act
        var portuguese = credit.ToShortText("Foto: {0}, {1}", PtBr);
        var english = credit.ToShortText("Photo: {0} ({1}), “{2}”, {3}", CultureInfo.InvariantCulture);

        // Assert
        portuguese.ShouldBe("Foto: Ana Silva, CC BY-SA 4.0");
        english.ShouldBe("Photo: Ana Silva (CC BY-SA 4.0), “Castelo”, https://example.org/p/1");
    }

    [Fact]
    public void The_full_text_uses_the_title_format_when_there_is_a_title_and_notes_a_change()
    {
        // Arrange
        var titled = ImageCredit.Create("Ana Silva", "Castelo", null, "CC BY 2.0", null, modified: true).ShouldBeSuccess();
        var untitled = ImageCredit.Create("Ana Silva", null, null, "CC0 1.0", null, modified: false).ShouldBeSuccess();

        // Act
        var full = titled.ToFullText(Portuguese, PtBr);
        var plain = untitled.ToFullText(Portuguese, PtBr);

        // Assert
        full.ShouldBe("“Castelo”, de Ana Silva, licença CC BY 2.0 (adaptada)");
        plain.ShouldBe("Foto de Ana Silva, licença CC0 1.0");
    }

    [Fact]
    public void The_full_text_can_name_the_source()
    {
        // Arrange
        var credit = ImageCredit.Create("Ana", null, "https://example.org/p/1", "Pixabay", null, false).ShouldBeSuccess();
        var formats = new ImageCreditFormats("{2} by {0}, {1} ({3})", "{0}, {1} ({3})", "{0}, modified");

        // Act
        var text = credit.ToFullText(formats, CultureInfo.InvariantCulture);

        // Assert
        text.ShouldBe("Ana, Pixabay (https://example.org/p/1)");
    }

    [Theory]
    [InlineData("CC BY 2.0", "https://creativecommons.org/licenses/by/2.0/")]
    [InlineData("cc by-sa 4.0", "https://creativecommons.org/licenses/by-sa/4.0/")]
    [InlineData(" CC  BY-NC-ND  3.0 ", "https://creativecommons.org/licenses/by-nc-nd/3.0/")]
    [InlineData("CC BY-NC-SA 2.5", "https://creativecommons.org/licenses/by-nc-sa/2.5/")]
    [InlineData("CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/")]
    [InlineData("Public Domain Mark 1.0", "https://creativecommons.org/publicdomain/mark/1.0/")]
    [InlineData("PDM 1.0", "https://creativecommons.org/publicdomain/mark/1.0/")]
    [InlineData("pdm 1.0", "https://creativecommons.org/publicdomain/mark/1.0/")]
    public void Common_creative_commons_codes_have_a_known_license_link(string code, string expected)
    {
        // Act
        var url = ImageLicense.KnownUrl(code);

        // Assert
        url.ShouldBe(WebUrl.Restore(expected));
    }

    [Theory]
    [InlineData("Pixabay")]
    [InlineData("Public domain")]
    [InlineData("CC BY 9.0")]
    [InlineData("CC BY-XY 4.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_codes_have_no_known_link(string? code)
    {
        // Act
        var url = ImageLicense.KnownUrl(code);

        // Assert
        url.ShouldBeNull();
    }

    [Fact]
    public void A_license_is_created_and_restored_on_its_own()
    {
        // Act
        var created = ImageLicense.Create(" CC BY 4.0 ", "https://creativecommons.org/licenses/by/4.0/").ShouldBeSuccess();
        var restored = ImageLicense.Restore("CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/");
        var refused = ImageLicense.Create(null, null, field: "photo.license");

        // Assert
        created.ShouldBe(restored);
        created.ToString().ShouldBe("CC BY 4.0");
        refused.ErrorDetails.ShouldHaveValidationError("photo.license.code", "License is required.");
    }

    [Fact]
    public void A_stored_credit_is_restored_without_rules_and_equals_the_created_one()
    {
        // Arrange
        var created = ImageCredit.Create("Ana", "Castelo", "https://example.org/p", "CC BY 2.0", null, true).ShouldBeSuccess();

        // Act
        var restored = ImageCredit.Restore("Ana", "Castelo", "https://example.org/p", ImageLicense.Restore("CC BY 2.0", null), true);
        var other = ImageCredit.Restore("Ana", "Castelo", "https://example.org/p", ImageLicense.Restore("CC BY 2.0", null), false);

        // Assert
        restored.ShouldBe(created);
        other.ShouldNotBe(created);
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        // Arrange
        var credit = ImageCredit.Create("Ana", null, null, "CC0 1.0", null, false).ShouldBeSuccess();

        // Act
        Action noFormat = () => credit.ToShortText(" ", PtBr);
        Action noProvider = () => credit.ToShortText("{0}", null!);
        Action noFormats = () => credit.ToFullText(null!, PtBr);
        Action blankFormat = () => _ = new ImageCreditFormats("{0}", " ", "{0}");
        Action noAuthor = () => ImageCredit.Restore(" ", null, null, ImageLicense.Restore("CC0 1.0", null), false);
        Action noLicense = () => ImageCredit.Restore("Ana", null, null, null!, false);
        Action noCode = () => ImageLicense.Restore(" ", null);

        // Assert
        noFormat.ShouldThrow<ArgumentException>();
        noProvider.ShouldThrow<ArgumentNullException>();
        noFormats.ShouldThrow<ArgumentNullException>();
        blankFormat.ShouldThrow<ArgumentException>();
        noAuthor.ShouldThrow<ArgumentException>();
        noLicense.ShouldThrow<ArgumentNullException>();
        noCode.ShouldThrow<ArgumentException>();
    }
}
