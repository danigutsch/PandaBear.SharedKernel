using ImageMagick;

namespace SharedKernel.ImageProcessing.Tests;

/// <summary>
/// Tests MagickImageProcessor.Process PNG output, which must be lossless, keep 8-bit alpha, and carry no metadata or timestamp chunks.
/// </summary>
public sealed class MagickImageProcessorPngOutputTests
{
    private static readonly string[] MetadataChunkTypes = ["tEXt", "zTXt", "iTXt", "tIME", "eXIf"];

    [Fact]
    public void Process_creates_resized_png_variant()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(320, 180);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("hero-png", ImageOutputFormat.Png, 160, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        variant.Name.ShouldBe("hero-png");
        variant.Format.ShouldBe(ImageOutputFormat.Png);
        variant.Width.ShouldBe(160);
        variant.Height.ShouldBe(90);
        TestImages.ReadFormat(variant.Content).ShouldBe(MagickFormat.Png);
    }

    [Fact]
    public void Process_keeps_transparent_and_semi_transparent_alpha_in_png_variants()
    {
        // Arrange
        var sourcePixels = PngTestImages.CreateGradientPixels();
        using var content = PngTestImages.CreatePng(sourcePixels, PngTestImages.GradientWidth, PngTestImages.GradientHeight);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("alpha", ImageOutputFormat.Png, PngTestImages.GradientWidth, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        variant.Width.ShouldBe(PngTestImages.GradientWidth);
        variant.Height.ShouldBe(PngTestImages.GradientHeight);
        var outputPixels = PngTestImages.ReadRgbaPixels(variant.Content);
        outputPixels.Length.ShouldBe(sourcePixels.Length);
        for (var index = 0; index < sourcePixels.Length; index += 4)
        {
            outputPixels[index + 3].ShouldBe(sourcePixels[index + 3], $"alpha of pixel {index / 4}");
            if (sourcePixels[index + 3] == 0)
            {
                continue;
            }

            for (var channel = 0; channel < 3; channel++)
            {
                Math.Abs(sourcePixels[index + channel] - outputPixels[index + channel])
                    .ShouldBeLessThanOrEqualTo(1, $"channel {channel} of pixel {index / 4}");
            }
        }
    }

    [Fact]
    public void Process_writes_resized_png_variants_with_eight_bit_rgba_when_alpha_exists()
    {
        // Arrange
        using var content = PngTestImages.CreatePng(
            PngTestImages.CreateGradientPixels(),
            PngTestImages.GradientWidth,
            PngTestImages.GradientHeight);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("alpha", ImageOutputFormat.Png, PngTestImages.GradientWidth / 2, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var (bitDepth, colorType) = PngTestImages.ReadHeader(result.Variants[0].Content);
        bitDepth.ShouldBe((byte)8);
        colorType.ShouldBe((byte)6);
    }

    [Fact]
    public void Process_strips_profiles_from_png_variants()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(64, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("png", ImageOutputFormat.Png, 32, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        TestImages.HasProfile(result.Variants[0].Content).ShouldBe(false);
    }

    [Fact]
    public void Process_writes_png_variants_without_text_time_or_exif_chunks()
    {
        // Arrange
        using var content = TestImages.CreateOrientedJpeg(64, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("png", ImageOutputFormat.Png, 32, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var chunkTypes = PngTestImages.ReadChunkTypes(result.Variants[0].Content);
        chunkTypes.ShouldContain("IHDR");
        chunkTypes.ShouldContain("IDAT");
        chunkTypes.Intersect(MetadataChunkTypes).ShouldBeEmpty();
    }

    [Fact]
    public void Process_writes_identical_png_bytes_for_identical_input_processed_at_different_times()
    {
        // Arrange
        var sourcePixels = PngTestImages.CreateGradientPixels();
        using var firstContent = PngTestImages.CreatePng(sourcePixels, PngTestImages.GradientWidth, PngTestImages.GradientHeight);
        using var secondContent = PngTestImages.CreatePng(sourcePixels, PngTestImages.GradientWidth, PngTestImages.GradientHeight);
        ImageVariantRequest[] variants = [new ImageVariantRequest("png", ImageOutputFormat.Png, PngTestImages.GradientWidth, 85)];

        // Act
        var first = MagickImageProcessor.Process(
            new ImageProcessingRequest(firstContent, variants, ImageProcessingLimits.WebDefault),
            TestContext.Current.CancellationToken);
        Thread.Sleep(TimeSpan.FromMilliseconds(1100));
        var second = MagickImageProcessor.Process(
            new ImageProcessingRequest(secondContent, variants, ImageProcessingLimits.WebDefault),
            TestContext.Current.CancellationToken);

        // Assert
        second.Variants[0].Content.ToArray().ShouldBe(first.Variants[0].Content.ToArray());
    }

    [Fact]
    public void Process_ignores_quality_for_lossless_png_variants()
    {
        // Arrange
        using var content = PngTestImages.CreatePng(
            PngTestImages.CreateGradientPixels(),
            PngTestImages.GradientWidth,
            PngTestImages.GradientHeight);
        var request = new ImageProcessingRequest(
            content,
            [
                new ImageVariantRequest("lowest", ImageOutputFormat.Png, PngTestImages.GradientWidth, 1),
                new ImageVariantRequest("highest", ImageOutputFormat.Png, PngTestImages.GradientWidth, 100)
            ],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        result.Variants[0].Content.ToArray().ShouldBe(result.Variants[1].Content.ToArray());
    }
}
