namespace PandaBear.InputNormalization.Tests;

/// <summary>Exercises format selection for known raster rendition types.</summary>
public sealed class RasterImageMediaTypeTests
{
    [Theory]
    [InlineData(" IMAGE/AVIF ", "image/avif", "avif")]
    [InlineData("image/jpeg", "image/jpeg", "jpg")]
    [InlineData("image/WebP", "image/webp", "webp")]
    [InlineData("image/png", "image/png", "png")]
    public void Finds_the_canonical_media_type_and_extension(string input, string mediaType, string extension)
    {
        // Act
        var format = RasterImageMediaType.Find(input);

        // Assert
        format.ShouldNotBeNull().MediaType.ShouldBe(mediaType);
        format.ShouldNotBeNull().FileExtension.ShouldBe(extension);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("image/svg+xml")]
    [InlineData("image/webp; charset=utf-8")]
    public void Unknown_media_types_have_no_rendition_format(string? input)
    {
        // Act
        var format = RasterImageMediaType.Find(input);

        // Assert
        format.ShouldBeNull();
    }
}
