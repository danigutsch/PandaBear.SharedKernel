using System.Buffers.Binary;
using System.Runtime.InteropServices;
using PandaBear.Testing;

namespace PandaBear.UploadSanitizing.Tests.Images;

/// <summary>
/// Tests that the sanitizer removes location, camera and text metadata from each accepted format, keeps the colour profile and the image
/// data byte for byte, and reads the size from the image header.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerFormatTests
{
    [Fact]
    public void A_jpeg_loses_exif_xmp_photoshop_comment_and_trailing_data_but_keeps_jfif_icc_and_the_image()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.JpegWithMetadata(640, 480), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(ImageFormat.Jpeg);
        (image.Width, image.Height).ShouldBe((640, 480));
        image.Bytes.ToArray().ShouldBe(ImageSamples.Jpeg(640, 480));
        ImageSamples.ContainsSecret(image.Bytes.Span).ShouldBeFalse();
        ImageSamples.ContainsProfile(image.Bytes.Span).ShouldBeTrue();
    }

    [Fact]
    public void A_jpeg_with_several_scans_loses_a_comment_between_them_and_keeps_both_scans()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.JpegWithTwoScans(), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        (image.Width, image.Height).ShouldBe((20, 10));
        image.Bytes.ToArray().ShouldBe(ImageSamples.JpegWithTwoScansClean());
    }

    [Fact]
    public void Fill_bytes_before_a_jpeg_marker_are_dropped()
    {
        // Arrange
        var jpeg = ImageSamples.Jpeg();
        var padded = ImageSamples.Concat(jpeg[..2], [0xFF, 0xFF], jpeg[2..]);

        // Act
        var image = ImageSanitizer.Sanitize(padded, ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Bytes.ToArray().ShouldBe(jpeg);
    }

    [Fact]
    public void A_png_loses_its_text_exif_time_chunks_and_trailing_data_but_keeps_its_colour_profile()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.PngWithMetadata(), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(ImageFormat.Png);
        (image.Width, image.Height).ShouldBe((1, 1));
        image.Bytes.ToArray().ShouldBe(ImageSamples.PngWithProfile());
        ImageSamples.ContainsSecret(image.Bytes.Span).ShouldBeFalse();
    }

    [Fact]
    public void A_webp_loses_its_exif_and_xmp_chunks_and_flags_keeps_icc_and_alpha_and_gets_a_correct_riff_size()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.WebpWithMetadata(), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(ImageFormat.Webp);
        (image.Width, image.Height).ShouldBe((1, 1));
        var bytes = image.Bytes.ToArray();
        ImageSamples.ContainsSecret(bytes).ShouldBeFalse();
        ImageSamples.ContainsProfile(bytes).ShouldBeTrue();
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)).ShouldBe(bytes.Length - 8);
        var exifAndXmpFlags = bytes[20] & 0x0C;
        var iccAndAlphaFlags = bytes[20] & 0x30;
        exifAndXmpFlags.ShouldBe(0);
        iccAndAlphaFlags.ShouldBe(0x30);
        bytes[^ImageSamples.Webp()[12..].Length..].ShouldBe(ImageSamples.Webp()[12..]);
    }

    [Fact]
    public void A_plain_lossless_webp_is_kept_as_it_is()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.Webp(), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Bytes.ToArray().ShouldBe(ImageSamples.Webp());
        (image.Width, image.Height).ShouldBe((1, 1));
    }

    [Fact]
    public void A_lossy_webp_takes_its_size_from_the_key_frame_header()
    {
        // Act
        var image = ImageSanitizer.Sanitize(ImageSamples.LossyWebp(1920, 1080), ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        (image.Width, image.Height).ShouldBe((1920, 1080));
        image.Bytes.ToArray().ShouldBe(ImageSamples.LossyWebp(1920, 1080));
    }

    [Theory]
    [MemberData(nameof(WithMetadata))]
    public void A_sanitized_image_is_accepted_again_unchanged_with_the_same_size(string because, byte[] file)
    {
        // Arrange
        var first = ImageSanitizer.Sanitize(file, ImageSanitizerOptions.Default).ShouldBeSuccess(because);

        // Act
        var again = ImageSanitizer.Sanitize(first.Bytes.Span, ImageSanitizerOptions.Default).ShouldBeSuccess(because);

        // Assert
        (again.Width, again.Height).ShouldBe((first.Width, first.Height), because);
        again.Bytes.ToArray().ShouldBe(first.Bytes.ToArray(), because);
        ImageSamples.ContainsSecret(again.Bytes.Span).ShouldBeFalse(because);
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg, ".jpg", "image/jpeg")]
    [InlineData(ImageFormat.Png, ".png", "image/png")]
    [InlineData(ImageFormat.Webp, ".webp", "image/webp")]
    public void The_extension_and_media_type_follow_the_recognized_format(ImageFormat format, string extension, string contentType)
    {
        // Arrange
        var file = format switch
        {
            ImageFormat.Jpeg => ImageSamples.Jpeg(),
            ImageFormat.Png => ImageSamples.Png(),
            _ => ImageSamples.Webp(),
        };

        // Act
        var image = ImageSanitizer.Sanitize(file, ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(format);
        image.FileExtension.ShouldBe(extension);
        image.ContentType.ShouldBe(contentType);
    }

    [Fact]
    public void Sanitizing_a_span_leaves_the_input_unchanged()
    {
        // Arrange
        var file = ImageSamples.JpegWithMetadata();
        var original = file.ToArray();

        // Act
        ImageSanitizer.Sanitize(file, ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        file.ShouldBe(original);
    }

    [Fact]
    public void Sanitizing_in_place_returns_a_slice_of_the_callers_buffer()
    {
        // Arrange
        var buffer = ImageSamples.WebpWithMetadata(1200, 800);

        // Act
        var image = ImageSanitizer.SanitizeInPlace(buffer, ImageSanitizerOptions.Default).ShouldBeSuccess();

        // Assert
        MemoryMarshal.TryGetArray(image.Bytes, out var segment).ShouldBeTrue();
        segment.Array.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(0);
        (image.Width, image.Height).ShouldBe((1200, 800));
        ImageSamples.ContainsSecret(image.Bytes.Span).ShouldBeFalse();
    }

    /// <summary>Gets an image of every format that carries metadata.</summary>
    public static TheoryData<string, byte[]> WithMetadata => new()
    {
        { "jpeg", ImageSamples.JpegWithMetadata(1600, 900) },
        { "progressive jpeg", ImageSamples.JpegWithTwoScans() },
        { "png", ImageSamples.PngWithMetadata() },
        { "webp", ImageSamples.WebpWithMetadata(1200, 800) },
    };
}
