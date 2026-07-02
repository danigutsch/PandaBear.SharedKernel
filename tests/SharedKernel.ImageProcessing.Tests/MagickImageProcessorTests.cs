using ImageMagick;
using SharedKernel.Testing.Assertions;

namespace SharedKernel.ImageProcessing.Tests;

public sealed class MagickImageProcessorTests
{
    [Fact]
    public void Process_creates_metadata_stripped_responsive_variants()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(320, 180);
        var request = new ImageProcessingRequest(
            content,
            [
                new ImageVariantRequest("hero-avif", ImageOutputFormat.Avif, 160, 75),
                new ImageVariantRequest("hero-webp", ImageOutputFormat.WebP, 120, 80),
                new ImageVariantRequest("hero-jpeg", ImageOutputFormat.Jpeg, 80, 85)
            ],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        result.Width.ShouldBe(320);
        result.Height.ShouldBe(180);
        result.Variants.Count.ShouldBe(3);

        var avif = result.Variants[0];
        avif.Name.ShouldBe("hero-avif");
        avif.Format.ShouldBe(ImageOutputFormat.Avif);
        avif.Width.ShouldBe(160);
        avif.Height.ShouldBe(90);
        TestImages.ReadFormat(avif.Content).ShouldBe(MagickFormat.Avif);
        TestImages.HasProfile(avif.Content).ShouldBe(false);

        var webp = result.Variants[1];
        webp.Width.ShouldBe(120);
        webp.Height.ShouldBe(68);
        TestImages.ReadFormat(webp.Content).ShouldBe(MagickFormat.WebP);
        TestImages.HasProfile(webp.Content).ShouldBe(false);

        var jpeg = result.Variants[2];
        jpeg.Width.ShouldBe(80);
        jpeg.Height.ShouldBe(45);
        TestImages.ReadFormat(jpeg.Content).ShouldBe(MagickFormat.Jpeg);
        TestImages.HasProfile(jpeg.Content).ShouldBe(false);
    }

    [Fact]
    public void Process_does_not_upscale_small_sources()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(48, 24);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("original", ImageOutputFormat.Jpeg, 400, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        variant.Width.ShouldBe(48);
        variant.Height.ShouldBe(24);
    }

    [Fact]
    public void Process_applies_exif_orientation_before_generating_variants()
    {
        // Arrange
        using var content = TestImages.CreateOrientedJpeg(40, 20);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("oriented", ImageOutputFormat.Jpeg, 40, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        result.Width.ShouldBe(20);
        result.Height.ShouldBe(40);

        var variant = result.Variants[0];
        variant.Width.ShouldBe(20);
        variant.Height.ShouldBe(40);
    }

    [Fact]
    public void Process_transforms_non_srgb_sources_before_stripping_profiles()
    {
        // Arrange
        using var content = TestImages.CreateCmykJpeg(64, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("srgb", ImageOutputFormat.Jpeg, 64, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        TestImages.ReadColorSpace(variant.Content).ShouldBe(ColorSpace.sRGB);
        TestImages.HasProfile(variant.Content).ShouldBe(false);
    }

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Avif)]
    public void Process_accepts_supported_upload_input_signatures(MagickFormat inputFormat)
    {
        // Arrange
        using var content = TestImages.CreateImage(64, 32, inputFormat);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.WebP, 32, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        variant.Width.ShouldBe(32);
        variant.Height.ShouldBe(16);
        TestImages.ReadFormat(variant.Content).ShouldBe(MagickFormat.WebP);
    }

    [Fact]
    public void Process_accepts_avif_sequence_input_signature()
    {
        // Arrange
        using var content = TestImages.CreateAvifWithMajorBrand("avis");
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.WebP, 32, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var variant = result.Variants[0];
        variant.Width.ShouldBe(32);
        variant.Height.ShouldBe(16);
        TestImages.ReadFormat(variant.Content).ShouldBe(MagickFormat.WebP);
    }

    [Fact]
    public void Process_creates_bounded_thumbnails_and_icon_variants()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(300, 150);
        var request = new ImageProcessingRequest(
            content,
            [
                new ImageVariantRequest("thumb", ImageOutputFormat.WebP, 64, 80, 64),
                new ImageVariantRequest("favicon", ImageOutputFormat.Ico, 32, 90, 32)
            ],
            ImageProcessingLimits.WebDefault);

        // Act
        var result = MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var thumbnail = result.Variants[0];
        thumbnail.Width.ShouldBe(64);
        thumbnail.Height.ShouldBe(32);
        TestImages.ReadFormat(thumbnail.Content).ShouldBe(MagickFormat.WebP);
        TestImages.HasProfile(thumbnail.Content).ShouldBe(false);

        var icon = result.Variants[1];
        icon.Width.ShouldBe(32);
        icon.Height.ShouldBe(32);
        TestImages.HasIcoHeader(icon.Content).ShouldBe(true);
    }

    [Fact]
    public void Process_requires_seekable_streams_for_pre_decode_probing()
    {
        // Arrange
        using var image = TestImages.CreateJpegWithProfile(32, 32);
        using var content = new NonSeekableStream(image.ToArray());
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("seekable", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_requires_streams_positioned_at_the_beginning()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        content.Position = 1;
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("beginning", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_empty_variant_requests()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(content, [], ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("At least one", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_invalid_limits()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            new ImageProcessingLimits(0, 32, 1_024));

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("limits", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_limits_that_exceed_process_resource_limits()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            new ImageProcessingLimits(8_001, 8_000, 40_000_000));

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("exceed", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_images_that_exceed_decoded_limits()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            new ImageProcessingLimits(31, 32, 1_024));

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("exceeds", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_images_that_exceed_pixel_count_limits()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            new ImageProcessingLimits(32, 32, 1_023));

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("exceeds", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_variant_without_name()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest(" ", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("name", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_variant_without_positive_dimensions()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 0, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("dimensions", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_variant_without_positive_height_when_height_is_supplied()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85, 0)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("dimensions", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_rejects_variant_quality_outside_supported_range()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 0)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("quality");
    }

    [Fact]
    public void Process_rejects_variant_quality_above_supported_range()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 101)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("quality");
    }

    [Fact]
    public void Process_rejects_unsupported_output_formats()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", (ImageOutputFormat)999, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("format");
        exception.ActualValue.ShouldBe((ImageOutputFormat)999);
    }

    [Fact]
    public void Process_rejects_unsupported_input_formats_before_decoding()
    {
        // Arrange
        using var content = new MemoryStream("%PDF-1.7"u8.ToArray());
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("unsupported", StringComparison.Ordinal);
    }

    [Fact]
    public void Process_honors_cancellation_before_generating_variants()
    {
        // Arrange
        using var content = TestImages.CreateJpegWithProfile(32, 32);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, cts.Token);

        // Assert
        act.ShouldThrow<OperationCanceledException>();
    }

    [Fact]
    public void Process_wraps_decoder_failures()
    {
        // Arrange
        using var content = new MemoryStream([0xFF, 0xD8, 0x01]);
        var request = new ImageProcessingRequest(
            content,
            [new ImageVariantRequest("thumb", ImageOutputFormat.Jpeg, 16, 85)],
            ImageProcessingLimits.WebDefault);

        // Act
        Action act = () => MagickImageProcessor.Process(request, TestContext.Current.CancellationToken);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("could not be decoded", StringComparison.Ordinal);
    }
}
