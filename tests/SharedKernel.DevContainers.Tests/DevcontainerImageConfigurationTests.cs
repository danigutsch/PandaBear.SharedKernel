using System.Text.Json;

namespace SharedKernel.DevContainers.Tests;

/// <summary>Tests the image-only configuration shared by repository scaffolds.</summary>
public sealed class DevcontainerImageConfigurationTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("alpine@sha256:" + Digest)]
    [InlineData("registry.example.test/namespace/dev_image:release.1@sha256:" + Digest)]
    [InlineData("[2001:db8::1]:5000/image@sha256:" + Digest)]
    public void TryCreate_writes_the_exact_pinned_reference_and_encodes_the_display_name(string image)
    {
        // Arrange
        const string name = "Example \"workspace\"";

        // Act
        var result = DevcontainerImageConfiguration.TryCreate(name, image, out var configuration);

        // Assert
        result.ShouldBeTrue();
        var content = configuration.ShouldNotBeNull();
        using var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("image").GetString().ShouldBe(image);
        json.RootElement.GetProperty("name").GetString().ShouldBe(name);
        content.ShouldEndWith("\n", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tools//image@sha256:" + Digest)]
    [InlineData("tools/image@sha256:" + Digest + "0")]
    public void TryCreate_leaves_no_configuration_when_the_image_is_invalid(string? image)
    {
        // Arrange
        const string name = "Example";

        // Act
        var result = DevcontainerImageConfiguration.TryCreate(name, image, out var configuration);

        // Assert
        result.ShouldBeFalse();
        configuration.ShouldBeNull();
    }
}
