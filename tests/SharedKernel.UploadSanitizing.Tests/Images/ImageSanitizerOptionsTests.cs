using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Images;

/// <summary>Tests the sanitizer options: their defaults and the values a caller cannot set.</summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerOptionsTests
{
    [Fact]
    public void The_defaults_are_ten_mebibytes_twenty_thousand_pixels_and_the_file_field()
    {
        // Act
        var options = ImageSanitizerOptions.Default;

        // Assert
        options.MaxBytes.ShouldBe(10 * 1024 * 1024);
        options.MaxDimension.ShouldBe(20_000);
        options.Field.ShouldBe("file");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(ImageSanitizerOptions.MaxBytesLimit + 1)]
    public void A_byte_limit_outside_the_accepted_range_is_refused(long maxBytes)
    {
        // Act
        Action act = () => _ = new ImageSanitizerOptions { MaxBytes = maxBytes };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_dimension_limit_below_one_is_refused()
    {
        // Act
        Action act = () => _ = new ImageSanitizerOptions { MaxDimension = 0 };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_field_is_refused(string field)
    {
        // Act
        Action act = () => _ = new ImageSanitizerOptions { Field = field };

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Null_options_are_rejected_by_every_entry_point()
    {
        // Act
        Action span = () => ImageSanitizer.Sanitize(ImageSamples.Png(), null!);
        Action inPlace = () => ImageSanitizer.SanitizeInPlace(ImageSamples.Png(), null!);

        // Assert
        span.ShouldThrow<ArgumentNullException>();
        inPlace.ShouldThrow<ArgumentNullException>();
    }
}
