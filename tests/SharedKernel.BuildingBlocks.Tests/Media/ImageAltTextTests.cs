using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the stored accessibility decision of an image: sanitized, length-capped alt text, or an explicit decorative mark.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class ImageAltTextTests
{
    [Theory]
    [InlineData("  Ciclistas   na estrada ", false, "Ciclistas na estrada")]
    [InlineData(null, true, "")]
    [InlineData("  ", true, "")]
    public void Alt_text_describes_an_image_with_sanitized_text_or_marks_it_decorative_without_text(string? text, bool decorative, string expected)
    {
        // Act
        var alt = ImageAltText.Create(text, decorative).ShouldBeSuccess();

        // Assert
        alt.Text.ShouldBe(expected);
        alt.IsDecorative.ShouldBe(decorative);
        alt.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Alt_text_is_required_unless_the_image_is_decorative(string? text)
    {
        // Act
        var result = ImageAltText.Create(text, decorative: false);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError("alt", "Alt text is required unless the image is decorative.");
    }

    [Theory]
    [InlineData("A bike")]
    [InlineData("Broken \uD800")]
    public void A_decorative_image_has_no_alt_text(string text)
    {
        // Act
        var result = ImageAltText.Create(text, decorative: true);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError("alt", "A decorative image has no alt text.");
    }

    [Fact]
    public void Alt_text_is_capped_and_must_be_valid_text()
    {
        // Act
        var longest = ImageAltText.Create(new string('a', ImageAltText.MaxLength), decorative: false);
        var tooLong = ImageAltText.Create(new string('a', ImageAltText.MaxLength + 1), decorative: false);
        var malformed = ImageAltText.Create("Broken \uDC00", decorative: false);

        // Assert
        longest.ShouldBeSuccess().Text.Length.ShouldBe(ImageAltText.MaxLength);
        tooLong.ErrorDetails.ShouldHaveValidationError("alt", "Alt text cannot exceed 500 characters.");
        malformed.ErrorDetails.ShouldHaveValidationError("alt", "Alt text is not valid text.");
    }

    [Fact]
    public void Errors_go_on_the_given_field_and_never_echo_the_input()
    {
        // Act
        var result = ImageAltText.Create("Secret <b>text</b>", decorative: true, field: "translations[en].alt");

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("translations[en].alt", "A decorative image has no alt text.");
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldNotContain("Secret", StringComparison.Ordinal);
    }

    [Fact]
    public void A_stored_value_is_restored_as_it_was_and_an_empty_one_is_decorative()
    {
        // Act
        var described = ImageAltText.Restore("A lake");
        var decorative = ImageAltText.Restore(string.Empty);
        var missing = () => ImageAltText.Restore(null!);

        // Assert
        described.ShouldBe(ImageAltText.Create("A lake", decorative: false).ShouldBeSuccess());
        described.IsDecorative.ShouldBeFalse();
        decorative.IsDecorative.ShouldBeTrue();
        missing.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("text");
    }
}
