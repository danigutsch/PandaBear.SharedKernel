using ImageMagick;

namespace PandaBear.ImageProcessing.Tests;

/// <summary>
/// Tests ImageAnimationDetector.Detect, which classifies PNG and WebP containers as static, animated, malformed or
/// unsupported from their chunk headers alone.
/// </summary>
public sealed class ImageAnimationDetectorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_encoded_png_as_static(bool viaStream)
    {
        // Arrange
        var content = TestImages.EncodeImage(16, 8, MagickFormat.Png);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_with_actl_before_idat_as_animated(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.InsertAfterIhdr(TestImages.EncodeImage(16, 8, MagickFormat.Png), ContainerBytes.Actl(2));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Animated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_with_actl_after_idat_as_static(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.Png(
            ContainerBytes.Ihdr(),
            ContainerBytes.PngChunk("IDAT", [0x78, 0x9C]),
            ContainerBytes.Actl(2),
            ContainerBytes.PngChunk("IEND", []));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_ignores_actl_appended_after_png_iend(bool viaStream)
    {
        // Arrange
        byte[] content = [.. TestImages.EncodeImage(16, 8, MagickFormat.Png), .. ContainerBytes.Actl(2)];

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_whose_first_chunk_is_not_ihdr_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.Png(
            ContainerBytes.Actl(2),
            ContainerBytes.Ihdr(),
            ContainerBytes.PngChunk("IDAT", [0x78, 0x9C]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_with_iend_before_idat_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.Png(
            ContainerBytes.Ihdr(),
            ContainerBytes.PngChunk("IEND", []),
            ContainerBytes.PngChunk("IDAT", [0x78, 0x9C]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_exceeding_the_chunk_walk_bound_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.PngWithAncillaryChunks(ImageAnimationDetector.MaxChunkCount);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_png_reaching_idat_at_the_last_walked_chunk_as_static(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.PngWithAncillaryChunks(ImageAnimationDetector.MaxChunkCount - 2);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false, 0xFFFFFFFFu)]
    [InlineData(true, 0xFFFFFFFFu)]
    [InlineData(false, 0x7FFFFFFFu)]
    [InlineData(true, 0x7FFFFFFFu)]
    [InlineData(false, 0x80000000u)]
    [InlineData(true, 0x80000000u)]
    public void Detect_reports_png_chunk_length_beyond_the_content_as_malformed(bool viaStream, uint declaredLength)
    {
        // Arrange
        var content = ContainerBytes.Png(
            ContainerBytes.Ihdr(),
            ContainerBytes.PngChunkHeader("acTL", declaredLength),
            ContainerBytes.PngChunk("IDAT", [0x78, 0x9C]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_encoded_webp_as_static(bool viaStream)
    {
        // Arrange
        var content = TestImages.EncodeImage(16, 8, MagickFormat.WebP);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_encoded_animated_webp_as_animated(bool viaStream)
    {
        // Arrange
        var content = TestImages.EncodeAnimatedWebP(16, 8);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Animated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_webp_with_vp8x_animation_flag_and_no_anim_chunk_as_animated(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.Vp8x(0x02), ContainerBytes.WebPChunk("VP8L", [0x2F, 0x00, 0x00]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Animated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_webp_with_clear_flag_and_anmf_chunk_as_animated(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.Vp8x(0x00), ContainerBytes.WebPChunk("ANMF", new byte[16]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Animated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_webp_with_clear_flag_and_no_animation_chunks_as_static(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.Vp8x(0x00), ContainerBytes.WebPChunk("VP8L", [0x2F, 0x00, 0x00]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_ignores_anmf_chunk_past_the_riff_end(bool viaStream)
    {
        // Arrange
        byte[] content =
        [
            .. ContainerBytes.WebP(ContainerBytes.Vp8x(0x00), ContainerBytes.WebPChunk("VP8L", [0x2F, 0x00, 0x00])),
            .. ContainerBytes.WebPChunk("ANMF", new byte[16])
        ];

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Static);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_webp_with_short_vp8x_payload_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.WebPChunk("VP8X", [0x02, 0, 0, 0, 0, 0, 0, 0, 0]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false, "ABCD")]
    [InlineData(true, "ABCD")]
    [InlineData(false, "ANIM")]
    [InlineData(true, "ANIM")]
    public void Detect_reports_webp_with_unknown_first_chunk_as_malformed(bool viaStream, string fourCc)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.WebPChunk(fourCc, new byte[6]));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_webp_with_empty_riff_body_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebP();

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false, 0xFFFFFFFFu)]
    [InlineData(true, 0xFFFFFFFFu)]
    [InlineData(false, 0xFFFFFFFEu)]
    [InlineData(true, 0xFFFFFFFEu)]
    public void Detect_reports_webp_chunk_size_beyond_the_riff_end_as_malformed(bool viaStream, uint declaredSize)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.Vp8x(0x00), ContainerBytes.WebPChunkHeader("ANMF", declaredSize));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false, 0xFFFFFFFFu)]
    [InlineData(true, 0xFFFFFFFFu)]
    [InlineData(false, 0xFFFFFFFEu)]
    [InlineData(true, 0xFFFFFFFEu)]
    public void Detect_reports_first_webp_chunk_size_beyond_the_riff_end_as_malformed(bool viaStream, uint declaredSize)
    {
        // Arrange
        var content = ContainerBytes.WebP(ContainerBytes.WebPChunkHeader("VP8X", declaredSize));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_riff_size_beyond_the_content_as_malformed(bool viaStream)
    {
        // Arrange
        var content = ContainerBytes.WebPWithRiffSize(0xFFFFFFFF, ContainerBytes.Vp8x(0x02));

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_jpeg_as_unsupported(bool viaStream)
    {
        // Arrange
        var content = TestImages.EncodeImage(16, 8, MagickFormat.Jpeg);

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Unsupported);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detect_reports_empty_content_as_unsupported(bool viaStream)
    {
        // Arrange
        byte[] content = [];

        // Act
        var status = AnimationDetection.Detect(content, viaStream);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Unsupported);
    }

    [Fact]
    public void Detect_never_reports_truncated_animated_png_as_static()
    {
        // Arrange
        var content = ContainerBytes.InsertAfterIhdr(TestImages.EncodeImage(16, 8, MagickFormat.Png), ContainerBytes.Actl(2));

        // Act
        var staticPrefixes = AnimationDetection.PrefixLengthsReporting(content, ImageAnimationStatus.Static);

        // Assert
        staticPrefixes.ShouldBeEmpty();
    }

    [Fact]
    public void Detect_never_reports_truncated_animated_webp_as_static()
    {
        // Arrange
        var content = TestImages.EncodeAnimatedWebP(16, 8);

        // Act
        var staticPrefixes = AnimationDetection.PrefixLengthsReporting(content, ImageAnimationStatus.Static);

        // Assert
        staticPrefixes.ShouldBeEmpty();
    }

    [Fact]
    public void Detect_never_reports_truncated_static_png_as_animated()
    {
        // Arrange
        var content = TestImages.EncodeImage(16, 8, MagickFormat.Png);

        // Act
        var animatedPrefixes = AnimationDetection.PrefixLengthsReporting(content, ImageAnimationStatus.Animated);

        // Assert
        animatedPrefixes.ShouldBeEmpty();
    }

    [Fact]
    public void Detect_never_reports_truncated_static_webp_as_animated()
    {
        // Arrange
        var content = TestImages.EncodeImage(16, 8, MagickFormat.WebP);

        // Act
        var animatedPrefixes = AnimationDetection.PrefixLengthsReporting(content, ImageAnimationStatus.Animated);

        // Assert
        animatedPrefixes.ShouldBeEmpty();
    }

    [Fact]
    public void Detect_rejects_null_streams()
    {
        // Arrange
        Stream content = null!;

        // Act
        Action act = () => ImageAnimationDetector.Detect(content);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Detect_rejects_non_seekable_streams()
    {
        // Arrange
        using var content = new UnsizedStream(TestImages.EncodeImage(16, 8, MagickFormat.Png));

        // Act
        Action act = () => ImageAnimationDetector.Detect(content);

        // Assert
        var exception = act.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("seekable", StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_reads_streams_from_the_current_position_and_restores_it()
    {
        // Arrange
        byte[] junk = [0xFF, 0xD8, 0xFF, 0x00, 0x01];
        var png = ContainerBytes.InsertAfterIhdr(TestImages.EncodeImage(16, 8, MagickFormat.Png), ContainerBytes.Actl(2));
        using var content = new MemoryStream([.. junk, .. png]);
        content.Position = junk.Length;

        // Act
        var status = ImageAnimationDetector.Detect(content);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Animated);
        content.Position.ShouldBe(junk.Length);
    }

    [Fact]
    public void Detect_restores_the_stream_position_after_malformed_content()
    {
        // Arrange
        var png = ContainerBytes.Png(ContainerBytes.Ihdr(), ContainerBytes.PngChunkHeader("tEXt", 0x7FFFFFFF));
        using var content = new MemoryStream([0x00, .. png]);
        content.Position = 1;

        // Act
        var status = ImageAnimationDetector.Detect(content);

        // Assert
        status.ShouldBe(ImageAnimationStatus.Malformed);
        content.Position.ShouldBe(1);
    }
}
