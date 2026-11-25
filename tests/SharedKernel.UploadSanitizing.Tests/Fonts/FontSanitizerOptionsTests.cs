using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Fonts;

/// <summary>Tests the font sanitizer options: their defaults and the values a caller cannot set.</summary>
[TestScope(TestScope.Unit)]
public sealed class FontSanitizerOptionsTests
{
    [Fact]
    public void The_defaults_are_two_mebibytes_and_the_file_field()
    {
        // Act
        var options = FontSanitizerOptions.Default;

        // Assert
        options.MaxBytes.ShouldBe(2 * 1024 * 1024);
        options.Field.ShouldBe("file");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(FontSanitizerOptions.MaxBytesLimit + 1)]
    public void A_byte_limit_outside_the_accepted_range_is_refused(long maxBytes)
    {
        // Act
        Action act = () => _ = new FontSanitizerOptions { MaxBytes = maxBytes };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_field_is_refused(string field)
    {
        // Act
        Action act = () => _ = new FontSanitizerOptions { Field = field };

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Null_options_are_rejected_by_every_entry_point()
    {
        // Act
        Action span = () => FontSanitizer.Sanitize(Woff2Samples.Font(), null!);
        Action inPlace = () => FontSanitizer.SanitizeInPlace(Woff2Samples.Font(), null!);

        // Assert
        span.ShouldThrow<ArgumentNullException>();
        inPlace.ShouldThrow<ArgumentNullException>();
    }
}
