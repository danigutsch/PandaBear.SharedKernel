using ImageMagick;

namespace PandaBear.ImageProcessing.Tests;

/// <summary>
/// Tests MagickImageProcessor.Probe, which reports the format, stored dimensions, and animation flag of an image from its headers.
/// </summary>
public sealed class MagickImageProcessorProbeTests
{
    [Theory]
    [InlineData(MagickFormat.Png, ImageInputFormat.Png)]
    [InlineData(MagickFormat.Jpeg, ImageInputFormat.Jpeg)]
    [InlineData(MagickFormat.WebP, ImageInputFormat.WebP)]
    [InlineData(MagickFormat.Avif, ImageInputFormat.Avif)]
    public void Probe_reports_format_dimensions_and_no_animation_for_static_images(MagickFormat inputFormat, ImageInputFormat expectedFormat)
    {
        // Arrange
        using var content = TestImages.CreateImage(64, 32, inputFormat);

        // Act
        var result = MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        result.Format.ShouldBe(expectedFormat);
        result.Width.ShouldBe(64);
        result.Height.ShouldBe(32);
        result.IsAnimated.ShouldBe(false);
    }

    [Theory]
    [InlineData(MagickFormat.WebP, ImageInputFormat.WebP)]
    [InlineData(MagickFormat.Avif, ImageInputFormat.Avif)]
    public void Probe_reports_animated_images(MagickFormat inputFormat, ImageInputFormat expectedFormat)
    {
        // Arrange
        using var content = TestImages.CreateAnimatedImage(16, 8, inputFormat);

        // Act
        var result = MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        result.Format.ShouldBe(expectedFormat);
        result.Width.ShouldBe(16);
        result.Height.ShouldBe(8);
        result.IsAnimated.ShouldBe(true);
    }

    [Fact]
    public void Probe_reports_animated_png_from_actl_chunk()
    {
        // Arrange
        using var staticPng = TestImages.CreateImage(16, 8, MagickFormat.Png);
        var apng = ContainerBytes.InsertAfterIhdr(staticPng.ToArray(), ContainerBytes.Actl(2));
        using var content = new MemoryStream(apng);

        // Act
        var result = MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        result.Format.ShouldBe(ImageInputFormat.Png);
        result.IsAnimated.ShouldBe(true);
    }

    [Fact]
    public void Probe_rejects_malformed_png_containers()
    {
        // Arrange
        using var staticPng = TestImages.CreateImage(16, 8, MagickFormat.Png);
        var malformed = ContainerBytes.InsertAfterIhdr(staticPng.ToArray(), ContainerBytes.PngChunkHeader("tEXt", 0x7FFF_FFFF));
        using var content = new MemoryStream(malformed);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        act.ShouldThrow<ImageProcessingException>();
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_fails_closed_when_the_png_chunk_walk_is_malformed()
    {
        // Arrange
        using var staticPng = TestImages.CreateImage(16, 8, MagickFormat.Png);
        var privateChunk = ContainerBytes.PngChunk("prVt", []);
        var excessChunks = Enumerable.Repeat(privateChunk, ImageAnimationDetector.MaxChunkCount).SelectMany(chunk => chunk).ToArray();
        var malformed = ContainerBytes.InsertAfterIhdr(staticPng.ToArray(), excessChunks);
        using var content = new MemoryStream(malformed);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("malformed", StringComparison.Ordinal);
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_reports_stored_dimensions_before_exif_orientation()
    {
        // Arrange
        using var content = TestImages.CreateOrientedJpeg(40, 20);

        // Act
        var result = MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        result.Format.ShouldBe(ImageInputFormat.Jpeg);
        result.Width.ShouldBe(40);
        result.Height.ShouldBe(20);
    }

    [Fact]
    public void Probe_rejects_images_that_exceed_limits()
    {
        // Arrange
        using var content = TestImages.CreateImage(64, 32, MagickFormat.Png);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, new ImageProcessingLimits(63, 32, 4_096));

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("limits", StringComparison.Ordinal);
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_rejects_images_that_exceed_pixel_count_limits()
    {
        // Arrange
        using var content = TestImages.CreateImage(32, 32, MagickFormat.Jpeg);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, new ImageProcessingLimits(32, 32, 1_023));

        // Assert
        act.ShouldThrow<ImageProcessingException>();
    }

    [Fact]
    public void Probe_rejects_invalid_limits()
    {
        // Arrange
        using var content = TestImages.CreateImage(32, 32, MagickFormat.Png);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, new ImageProcessingLimits(0, 32, 1_024));

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void Probe_rejects_unsupported_input_signatures()
    {
        // Arrange
        using var content = new MemoryStream("%PDF-1.7"u8.ToArray());

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.Message.ShouldContain("unsupported", StringComparison.Ordinal);
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_wraps_decoder_failures()
    {
        // Arrange
        using var content = new MemoryStream([0xFF, 0xD8, 0x01]);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ImageProcessingException>();
        exception.InnerException.ShouldBeAssignableTo<MagickException>();
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_requires_seekable_streams()
    {
        // Arrange
        using var image = TestImages.CreateImage(32, 32, MagickFormat.Png);
        using var content = new NonSeekableStream(image.ToArray());

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Probe_requires_streams_positioned_at_the_beginning()
    {
        // Arrange
        using var content = TestImages.CreateImage(32, 32, MagickFormat.Png);
        content.Position = 1;

        // Act
        Action act = () => MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("content");
        content.Position.ShouldBe(1);
    }

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Avif)]
    public void Probe_restores_stream_position_after_success(MagickFormat inputFormat)
    {
        // Arrange
        using var content = TestImages.CreateImage(32, 16, inputFormat);

        // Act
        MagickImageProcessor.Probe(content, ImageProcessingLimits.WebDefault);

        // Assert
        content.Position.ShouldBe(0);
    }

    [Fact]
    public void Probe_rejects_null_content()
    {
        // Act
        Action act = () => MagickImageProcessor.Probe(null!, ImageProcessingLimits.WebDefault);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Probe_rejects_null_limits()
    {
        // Arrange
        using var content = TestImages.CreateImage(32, 32, MagickFormat.Png);

        // Act
        Action act = () => MagickImageProcessor.Probe(content, null!);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe("limits");
    }
}
