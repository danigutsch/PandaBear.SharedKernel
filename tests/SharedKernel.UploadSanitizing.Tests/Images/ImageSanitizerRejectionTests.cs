using PandaBear.Results;
using PandaBear.Testing;

namespace PandaBear.UploadSanitizing.Tests.Images;

/// <summary>
/// Tests that the sanitizer recognizes the format by the bytes alone and rejects, with a validation result rather than an exception, every
/// file that is not exactly well formed, too large, or too wide or tall.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerRejectionTests
{
    [Theory]
    [MemberData(nameof(NotImages))]
    public void A_file_whose_bytes_are_not_jpeg_png_or_webp_is_rejected_whatever_its_name(string because, byte[] bytes)
    {
        // Act
        var result = ImageSanitizer.Sanitize(bytes, ImageSanitizerOptions.Default);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.NotAnImageMessage);
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_malformed_image_is_rejected(string because, byte[] bytes)
    {
        // Act
        var result = ImageSanitizer.Sanitize(bytes, ImageSanitizerOptions.Default);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.MalformedMessage);
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_malformed_image_is_rejected_in_place_too(string because, byte[] bytes)
    {
        // Act
        var result = ImageSanitizer.SanitizeInPlace(bytes, ImageSanitizerOptions.Default);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.MalformedMessage);
    }

    [Fact]
    public void A_file_larger_than_the_limit_is_rejected_and_one_at_the_limit_is_accepted()
    {
        // Arrange
        var jpeg = ImageSamples.Jpeg();
        var exact = ImageSanitizerOptions.Default with
        {
            MaxBytes = jpeg.Length
        };
        var smaller = ImageSanitizerOptions.Default with
        {
            MaxBytes = jpeg.Length - 1
        };

        // Act
        var accepted = ImageSanitizer.Sanitize(jpeg, exact);
        var rejected = ImageSanitizer.Sanitize(jpeg, smaller);
        var rejectedInPlace = ImageSanitizer.SanitizeInPlace(jpeg.ToArray(), smaller);

        // Assert
        accepted.ShouldBeSuccess();
        rejected.ShouldBeRejected(ImageSanitizer.TooLargeMessage);
        rejectedInPlace.ShouldBeRejected(ImageSanitizer.TooLargeMessage);
    }

    [Theory]
    [InlineData(4000, 3000)]
    [InlineData(3000, 4000)]
    public void An_image_wider_or_taller_than_the_limit_is_rejected_and_one_at_the_limit_is_accepted(int width, int height)
    {
        // Arrange
        var options = ImageSanitizerOptions.Default with
        {
            MaxDimension = 3999
        };
        var atLimit = ImageSanitizerOptions.Default with
        {
            MaxDimension = 4000
        };

        // Act
        var rejected = ImageSanitizer.Sanitize(ImageSamples.Jpeg(width, height), options);
        var accepted = ImageSanitizer.Sanitize(ImageSamples.Jpeg(width, height), atLimit);

        // Assert
        rejected.ShouldBeRejected(ImageSanitizer.DimensionsTooLargeMessage);
        accepted.ShouldBeSuccess();
    }

    [Fact]
    public void The_errors_are_reported_on_the_configured_field()
    {
        // Arrange
        var options = ImageSanitizerOptions.Default with
        {
            Field = "cover"
        };

        // Act
        var result = ImageSanitizer.Sanitize("GIF89a"u8, options);

        // Assert
        result.ShouldBeRejected(ImageSanitizer.NotAnImageMessage, "cover");
    }

    [Theory]
    [MemberData(nameof(HugeDeclaredLengths))]
    public void A_huge_declared_length_is_rejected_without_a_matching_allocation(string because, byte[] bytes)
    {
        // Arrange
        ImageSanitizer.SanitizeInPlace(bytes.ToArray(), ImageSanitizerOptions.Default);
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var result = ImageSanitizer.SanitizeInPlace(bytes, ImageSanitizerOptions.Default);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBeLessThan(4096, because);
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.MalformedMessage);
    }

    /// <summary>Gets files that are not images of an accepted format.</summary>
    public static TheoryData<string, byte[]> NotImages => new()
    {
        { "an empty file", [] },
        { "a lone JPEG start marker", [0xFF, 0xD8] },
        { "a cut PNG signature", ImageSamples.Png()[..7] },
        { "a RIFF header too short for its type", ImageSamples.Ascii("RIFF\x04\0\0\0WEB") },
        { "an SVG", ImageSamples.Ascii("<svg xmlns=\"http://www.w3.org/2000/svg\"/>") },
        { "a GIF", ImageSamples.Ascii("GIF89a\x01\0\x01\0") },
        { "a PDF", ImageSamples.Ascii("%PDF-1.7") },
        { "a WAVE file", ImageSamples.Ascii("RIFF\x04\0\0\0WAVE") },
        { "an HTML page", ImageSamples.Ascii("<html><script>alert(1)</script></html>") },
    };

    /// <summary>Gets files that start like an accepted format but are damaged or not supported.</summary>
    public static TheoryData<string, byte[]> Malformed
    {
        get
        {
            var jpeg = ImageSamples.Jpeg();
            var png = ImageSamples.Png();
            var webp = ImageSamples.Webp();
            var afterHeader = ImageSamples.PngAfterHeader;
            return new TheoryData<string, byte[]>
            {
                { "a JPEG without a frame header", ImageSamples.Concat([0xFF, 0xD8], ImageSamples.Scan(), [0xFF, 0xD9]) },
                { "a JPEG without an end marker", jpeg[..^2] },
                { "a JPEG whose scan runs to the end of the file", ImageSamples.Concat(jpeg[..^2], [0x00, 0x01]) },
                { "a JPEG that ends in a lone 0xFF inside a scan", ImageSamples.Concat(jpeg[..^2], [0xFF]) },
                { "a JPEG that ends in fill bytes", ImageSamples.Concat(jpeg[..20], [0xFF, 0xFF]) },
                { "a JPEG that ends before a segment length", ImageSamples.Concat(jpeg[..20], [0xFF, 0xDB, 0x00]) },
                { "a JPEG with an end marker before any scan", ImageSamples.Concat(jpeg[..20], [0xFF, 0xD9]) },
                { "a JPEG segment longer than the file", ImageSamples.Concat([0xFF, 0xD8, 0xFF, 0xE1, 0x40, 0x00], ImageSamples.Ascii("Exif")) },
                { "a JPEG segment of the largest length in a short file", ImageSamples.Concat([0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF], ImageSamples.Ascii("Exif")) },
                { "a JPEG segment length of zero", ImageSamples.Concat([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00], jpeg[2..]) },
                { "a JPEG segment length of one", ImageSamples.Concat([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01], jpeg[2..]) },
                { "a JPEG of zero width", ImageSamples.Jpeg(0, 480) },
                { "a JPEG of zero height", ImageSamples.Jpeg(640, 0) },
                { "a JPEG frame header too short for the size", ImageSamples.Concat([0xFF, 0xD8], ImageSamples.JpegSegment(0xC0, [8, 0, 1]), ImageSamples.Scan(), [0xFF, 0xD9]) },
                { "a JPEG with two frame headers", ImageSamples.Concat(jpeg[..^2][..^ImageSamples.Scan().Length], ImageSamples.Frame(10, 10), ImageSamples.Scan(), [0xFF, 0xD9]) },
                { "a JPEG with a start-of-image marker inside", ImageSamples.Concat(jpeg[..20], [0xFF, 0xD8], jpeg[20..]) },
                { "a JPEG with a restart marker outside a scan", ImageSamples.Concat(jpeg[..20], [0xFF, 0xD0], jpeg[20..]) },
                { "a JPEG with a reserved marker", ImageSamples.Concat(jpeg[..20], [0xFF, 0x01], jpeg[20..]) },
                { "a JPEG with garbage between segments", ImageSamples.Concat(jpeg[..20], [0x00], jpeg[20..]) },
                { "a PNG cut inside a chunk", png[..40] },
                { "a PNG without its end chunk", png[..^12] },
                { "a PNG of only its signature and header", png[..afterHeader] },
                { "a PNG whose first chunk is not the header", ImageSamples.Concat(png[..8], png[afterHeader..]) },
                { "a PNG with two header chunks", ImageSamples.Concat(png[..afterHeader], png[8..afterHeader], png[afterHeader..]) },
                { "a PNG header chunk of the wrong length", ImageSamples.WithUInt32BigEndian(png, 8, 12) },
                { "a PNG without image data", ImageSamples.Concat(png[..afterHeader], png[^12..]) },
                { "a PNG chunk type with a digit", ImageSamples.Concat(png[..afterHeader], ImageSamples.PngChunk("tEX1", [1]), png[afterHeader..]) },
                { "a PNG chunk length that overflows", ImageSamples.WithUInt32BigEndian(png, afterHeader, 0xFFFFFFFF) },
                { "a PNG chunk length just under the signed limit", ImageSamples.WithUInt32BigEndian(png, afterHeader, 0x7FFFFFFF) },
                { "a PNG of negative width", ImageSamples.WithUInt32BigEndian(png, 16, 0x80000000) },
                { "a PNG of zero height", ImageSamples.WithUInt32BigEndian(png, 20, 0) },
                { "a WebP whose RIFF size does not match the file", ImageSamples.Concat(webp, [0, 0]) },
                { "a WebP whose RIFF size is the largest value", ImageSamples.WithUInt32LittleEndian(webp, 4, 0xFFFFFFFF) },
                { "a WebP whose chunk is longer than the file", ImageSamples.Riff(ImageSamples.Concat(ImageSamples.Ascii("WEBPVP8L"), BitConverter.GetBytes(999))) },
                { "a WebP chunk size that overflows when padded", ImageSamples.Riff(ImageSamples.Concat(ImageSamples.Ascii("WEBPVP8L"), BitConverter.GetBytes(uint.MaxValue))) },
                { "a WebP chunk header cut short", ImageSamples.Riff(ImageSamples.Concat(webp[8..], ImageSamples.Ascii("EXIF"))) },
                { "a WebP odd-sized chunk without its padding byte", ImageSamples.Riff(ImageSamples.Concat(webp[8..], ImageSamples.Ascii("EXIF"), BitConverter.GetBytes(1), [0x41])) },
                { "a WebP without image data", ImageSamples.Riff(ImageSamples.Ascii("WEBP")) },
                { "a WebP whose first chunk is metadata", ImageSamples.Riff(ImageSamples.Concat(ImageSamples.Ascii("WEBP"), ImageSamples.RiffChunk("EXIF", [1, 2]), webp[12..])) },
                { "a WebP extended header that is not first", ImageSamples.Riff(ImageSamples.Concat(webp[8..], ImageSamples.RiffChunk("VP8X", new byte[10]))) },
                { "a WebP extended header too short", ImageSamples.Riff(ImageSamples.Concat(ImageSamples.Ascii("WEBP"), ImageSamples.RiffChunk("VP8X", new byte[8]), webp[12..])) },
                { "a WebP lossless chunk with a wrong signature", ImageSamples.Corrupt(webp, 20, 0x00) },
                { "a WebP lossless chunk too short for the size", ImageSamples.Riff(ImageSamples.Concat(ImageSamples.Ascii("WEBP"), ImageSamples.RiffChunk("VP8L", [0x2F, 0, 0, 0]))) },
                { "a WebP lossy chunk with a wrong start code", ImageSamples.Corrupt(ImageSamples.LossyWebp(10, 10), 23, 0x00) },
                { "a WebP lossy chunk of zero width", ImageSamples.LossyWebp(0, 10) },
            };
        }
    }

    /// <summary>Gets small files whose chunk or segment lengths claim far more data than they hold.</summary>
    public static TheoryData<string, byte[]> HugeDeclaredLengths => new()
    {
        { "png", ImageSamples.WithUInt32BigEndian(ImageSamples.Png(), ImageSamples.PngAfterHeader, 0x7FFFFFF0) },
        { "webp", ImageSamples.WithUInt32LittleEndian(ImageSamples.Webp(), 16, 0x7FFFFFF0) },
        { "jpeg", ImageSamples.Concat([0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF], ImageSamples.Ascii("Exif")) },
    };
}
