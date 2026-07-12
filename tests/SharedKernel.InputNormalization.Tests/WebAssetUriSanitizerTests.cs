using SharedKernel.Testing;

namespace SharedKernel.InputNormalization.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.InputNormalizationCapability)]
public sealed class WebAssetUriSanitizerTests
{
    [Theory]
    [InlineData("https://cdn.example.test/logo.svg", "https://cdn.example.test/logo.svg")]
    [InlineData("  /images/logo.svg  ", "/images/logo.svg")]
    public void NormalizeRootRelativeOrHttps_accepts_safe_uris(string candidate, string expected)
    {
        // Arrange
        const int maxLength = 2048;

        // Act
        var normalized = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(candidate, maxLength);
        var uri = WebAssetUriSanitizer.ToRootRelativeOrHttpsUri(candidate, maxLength);

        // Assert
        normalized.ShouldBe(expected);
        uri.ShouldNotBeNull().OriginalString.ShouldBe(expected);
    }

    [Theory]
    [InlineData("http://example.test/logo.svg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("//cdn.example.test/logo.svg")]
    [InlineData("https://user:password@cdn.example.test/logo.svg")]
    [InlineData("https://cdn.example.test\\logo.svg")]
    [InlineData("logo.svg")]
    [InlineData("/images/my logo.svg")]
    [InlineData("/images\\logo.svg")]
    [InlineData("/images/logo\0.svg")]
    [InlineData("https://cdn.example.test/logo\0.svg")]
    [InlineData("https://cdn.example.test/logo\u001F.svg")]
    public void NormalizeRootRelativeOrHttps_rejects_unsafe_uris(string candidate)
    {
        // Arrange
        const int maxLength = 2048;

        // Act
        var normalized = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(candidate, maxLength);
        var uri = WebAssetUriSanitizer.ToRootRelativeOrHttpsUri(candidate, maxLength);

        // Assert
        normalized.ShouldBeNull();
        uri.ShouldBeNull();
    }

    [Fact]
    public void NormalizeRootRelativeOrHttps_rejects_overlong_uri()
    {
        // Arrange
        const int maxLength = 2048;
        var candidate = $"/{new string('a', maxLength)}";

        // Act
        var normalized = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(candidate, maxLength);

        // Assert
        normalized.ShouldBeNull();
    }
}
