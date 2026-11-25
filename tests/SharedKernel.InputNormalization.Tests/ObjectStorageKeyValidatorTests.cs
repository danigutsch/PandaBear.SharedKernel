using SharedKernel.Testing;

namespace SharedKernel.InputNormalization.Tests;

/// <summary>Unit tests for ObjectStorageKeyValidator.IsValidRelativeKey.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class ObjectStorageKeyValidatorTests
{
    [Theory]
    [InlineData("media/source.jpg")]
    [InlineData("media/2026/source.avif")]
    [InlineData("media/caf\u00E9-\uD83D\uDE00.jpg")]
    public void IsValidRelativeKey_accepts_safe_slash_delimited_keys(string candidate)
    {
        // Arrange
        const int maxLength = 1024;

        // Act
        var isValid = ObjectStorageKeyValidator.IsValidRelativeKey(candidate, maxLength);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/media/source.jpg")]
    [InlineData("\\media\\source.jpg")]
    [InlineData("media\\source.jpg")]
    [InlineData("C:/media/source.jpg")]
    [InlineData("media//source.jpg")]
    [InlineData("media/./source.jpg")]
    [InlineData("media/../source.jpg")]
    [InlineData("media/source\0.jpg")]
    [InlineData("media/source\n.jpg")]
    [InlineData("media/source\r.jpg")]
    [InlineData("media/source\t.jpg")]
    public void IsValidRelativeKey_rejects_unsafe_keys(string? candidate)
    {
        // Arrange
        const int maxLength = 1024;

        // Act
        var isValid = ObjectStorageKeyValidator.IsValidRelativeKey(candidate, maxLength);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValidRelativeKey_rejects_malformed_surrogates()
    {
        // Arrange
        // Lone surrogates are built in code: attribute arguments are stored as UTF-8 and would arrive as U+FFFD.
        const int maxLength = 1024;
        string[] candidates = ["media/source\uD800.jpg", "media/source\uDC00.jpg", "media/source\uDE00\uD83D.jpg", "media/source\uD83D"];

        // Act
        var results = candidates.Select(static candidate => ObjectStorageKeyValidator.IsValidRelativeKey(candidate, maxLength)).ToList();

        // Assert
        results.ShouldBe([false, false, false, false]);
    }

    [Fact]
    public void IsValidRelativeKey_rejects_invalid_length_limits()
    {
        // Arrange
        var overlongKey = new string('a', 1025);

        // Act
        var exceedsMaximum = ObjectStorageKeyValidator.IsValidRelativeKey(overlongKey, 1024);
        var hasInvalidMaximum = ObjectStorageKeyValidator.IsValidRelativeKey("media/source.jpg", 0);

        // Assert
        exceedsMaximum.ShouldBeFalse();
        hasInvalidMaximum.ShouldBeFalse();
    }

    [Fact]
    public void IsValidRelativeKey_rejects_unpaired_surrogates_that_cannot_round_trip_through_utf8()
    {
        // Arrange
        var candidate = "media/" + (char)0xd800 + ".jpg";

        // Act
        var valid = ObjectStorageKeyValidator.IsValidRelativeKey(candidate, 1024);

        // Assert
        valid.ShouldBeFalse();
    }

    [Fact]
    public void IsValidRelativeKey_preserves_well_formed_unicode_and_spaces()
    {
        // Arrange
        const string candidate = "media/café \U0001f43b.jpg";

        // Act
        var valid = ObjectStorageKeyValidator.IsValidRelativeKey(candidate, 1024);

        // Assert
        valid.ShouldBeTrue();
    }
}
