using SharedKernel.Testing;

namespace SharedKernel.InputNormalization.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class ObjectStorageKeyValidatorTests
{
    [Theory]
    [InlineData("media/source.jpg")]
    [InlineData("media/2026/source.avif")]
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
}
