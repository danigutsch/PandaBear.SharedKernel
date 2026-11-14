using PandaBear.Results;
using PandaBear.Testing;

namespace PandaBear.UploadSanitizing.Tests.Images;

/// <summary>
/// Tests that AVIF is refused unless <see cref="ImageSanitizerOptions.AllowAvif"/> is set, and that an allowed AVIF is recognized by its
/// <c>ftyp</c> brands, sized from the <c>ispe</c> of its primary item, checked box by box, and returned unchanged with
/// <see cref="SanitizedImage.MetadataRemoved"/> false.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerAvifTests
{
    private static readonly ImageSanitizerOptions AllowAvif = ImageSanitizerOptions.Default with
    {
        AllowAvif = true
    };

    [Fact]
    public void An_avif_is_not_accepted_by_default()
    {
        // Act
        var result = ImageSanitizer.Sanitize(AvifSamples.Avif(), ImageSanitizerOptions.Default);

        // Assert
        result.ShouldBeRejected(ImageSanitizer.NotAnImageMessage);
    }

    [Fact]
    public void An_allowed_avif_is_kept_unchanged_sized_from_its_primary_item_and_says_its_metadata_was_not_removed()
    {
        // Arrange
        var file = AvifSamples.Avif(1920, 1080);

        // Act
        var image = ImageSanitizer.Sanitize(file, AllowAvif).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(ImageFormat.Avif);
        (image.Width, image.Height).ShouldBe((1920, 1080));
        image.Bytes.ToArray().ShouldBe(file);
        image.MetadataRemoved.ShouldBeFalse();
        image.IsAnimated.ShouldBeFalse();
        image.FileExtension.ShouldBe(".avif");
        image.ContentType.ShouldBe("image/avif");
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Webp)]
    public void The_other_formats_say_their_metadata_was_removed(ImageFormat format)
    {
        // Arrange
        var file = format switch
        {
            ImageFormat.Jpeg => ImageSamples.Jpeg(),
            ImageFormat.Png => ImageSamples.Png(),
            _ => ImageSamples.Webp(),
        };

        // Act
        var image = ImageSanitizer.Sanitize(file, AllowAvif).ShouldBeSuccess();

        // Assert
        image.MetadataRemoved.ShouldBeTrue();
    }

    [Fact]
    public void An_avif_brand_among_the_compatible_brands_is_recognized()
    {
        // Act
        var image = ImageSanitizer.Sanitize(AvifSamples.Avif(10, 20, "mif1", "mif1", "avif"), AllowAvif).ShouldBeSuccess();

        // Assert
        image.Format.ShouldBe(ImageFormat.Avif);
        (image.Width, image.Height).ShouldBe((10, 20));
    }

    [Fact]
    public void The_size_comes_from_the_property_associated_with_the_primary_item()
    {
        // Act
        var image = ImageSanitizer.Sanitize(AvifSamples.AvifWithTwoItems((800, 600), (160, 120)), AllowAvif).ShouldBeSuccess();

        // Assert
        (image.Width, image.Height).ShouldBe((800, 600));
    }

    [Fact]
    public void An_avif_with_any_item_larger_than_the_limit_is_rejected()
    {
        // Arrange
        var options = AllowAvif with
        {
            MaxDimension = 1000
        };

        // Act
        var primaryTooLarge = ImageSanitizer.Sanitize(AvifSamples.Avif(1001, 10), options);
        var otherTooLarge = ImageSanitizer.Sanitize(AvifSamples.AvifWithTwoItems((800, 600), (10, 5000)), options);
        var atLimit = ImageSanitizer.Sanitize(AvifSamples.Avif(1000, 1000), options);

        // Assert
        primaryTooLarge.ShouldBeRejected(ImageSanitizer.DimensionsTooLargeMessage);
        otherTooLarge.ShouldBeRejected(ImageSanitizer.DimensionsTooLargeMessage);
        atLimit.ShouldBeSuccess();
    }

    [Fact]
    public void An_iso_media_file_of_another_brand_is_not_an_image()
    {
        // Arrange
        var mp4 = AvifSamples.File(AvifSamples.Ftyp("isom", "isom", "mp41"), AvifSamples.Box("moov", []), AvifSamples.Mdat());

        // Act
        var result = ImageSanitizer.Sanitize(mp4, AllowAvif);

        // Assert
        result.ShouldBeRejected(ImageSanitizer.NotAnImageMessage);
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_malformed_avif_is_rejected(string because, byte[] bytes)
    {
        // Act
        var result = ImageSanitizer.Sanitize(bytes, AllowAvif);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid, because)
            .ShouldHaveValidationError("file", ImageSanitizer.MalformedMessage);
    }

    [Fact]
    public void A_box_that_runs_to_the_end_of_the_file_and_a_box_with_a_64_bit_size_are_accepted()
    {
        // Arrange
        var mdat = AvifSamples.Mdat();
        var toEnd = AvifSamples.File(AvifSamples.Ftyp("avif", "avif"), AvifSamples.Meta(AvifSamples.Pitm(1), AvifSamples.Iprp(5, 6)), AvifSamples.UInt32(0), mdat[4..]);
        var largeSize = AvifSamples.File(
            AvifSamples.Ftyp("avif", "avif"),
            AvifSamples.Meta(AvifSamples.Pitm(1), AvifSamples.Iprp(5, 6)),
            AvifSamples.UInt32(1),
            mdat[4..8],
            [0, 0, 0, 0, .. AvifSamples.UInt32((uint)(mdat.Length + 8))],
            mdat[8..]);

        // Act
        var first = ImageSanitizer.Sanitize(toEnd, AllowAvif);
        var second = ImageSanitizer.Sanitize(largeSize, AllowAvif);

        // Assert
        first.ShouldBeSuccess();
        second.ShouldBeSuccess();
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_truncation_and_single_byte_change_of_an_avif_is_answered_with_a_result(string name, byte[] file)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(file);
        var answered = 0;

        // Act
        for (var length = 0; length <= file.Length; length++)
        {
            ImageSanitizer.Sanitize(file.AsSpan(0, length), AllowAvif).ShouldBeSuccessOrRejected(length);
            answered++;
        }

        for (var index = 0; index < file.Length; index++)
        {
            foreach (var value in (byte[])[0x00, 0x01, 0x7F, 0x80, 0xFF])
            {
                ImageSanitizer.SanitizeInPlace(ImageSamples.Corrupt(file, index, value), AllowAvif).ShouldBeSuccessOrRejected(file.Length);
                answered++;
            }
        }

        // Assert
        answered.ShouldBe((file.Length + 1) + (file.Length * 5), name);
    }

    /// <summary>Gets valid AVIF files of every structure the sanitizer reads.</summary>
    public static TheoryData<string, byte[]> Samples => new()
    {
        { "still", AvifSamples.Avif() },
        { "two items", AvifSamples.AvifWithTwoItems((80, 60), (16, 12)) },
        { "sequence", AvifSamples.AnimatedAvif() },
    };

    /// <summary>Gets files that start like an AVIF but are damaged or not supported.</summary>
    public static TheoryData<string, byte[]> Malformed
    {
        get
        {
            var ftyp = AvifSamples.Ftyp("avif", "avif", "mif1");
            var meta = AvifSamples.Meta(AvifSamples.Pitm(1), AvifSamples.Iprp(64, 48));
            var mdat = AvifSamples.Mdat();
            var avif = AvifSamples.Avif();
            return new TheoryData<string, byte[]>
            {
                { "an AVIF without a meta box", AvifSamples.File(ftyp, mdat) },
                { "an AVIF with two meta boxes", AvifSamples.File(ftyp, meta, meta, mdat) },
                { "an AVIF without a primary item", AvifSamples.File(ftyp, AvifSamples.Meta(AvifSamples.Iprp(64, 48)), mdat) },
                { "an AVIF without item properties", AvifSamples.File(ftyp, AvifSamples.Meta(AvifSamples.Pitm(1)), mdat) },
                { "an AVIF whose primary item has no size property", AvifSamples.File(ftyp, AvifSamples.Meta(AvifSamples.Pitm(2), AvifSamples.Iprp(64, 48)), mdat) },
                { "an AVIF of zero width", AvifSamples.Avif(0, 48) },
                { "an AVIF of zero height", AvifSamples.Avif(64, 0) },
                { "an AVIF wider than a signed integer", AvifSamples.WithProperty(AvifSamples.FullBox("ispe", 0, 0, ImageSamples.Concat(AvifSamples.UInt32(0x80000000), AvifSamples.UInt32(1))), entries: 1, index: 1) },
                { "an AVIF whose association names a missing property", AvifSamples.WithProperty(AvifSamples.Ispe(1, 1), entries: 1, index: 5) },
                { "an AVIF size property too short", AvifSamples.WithProperty(AvifSamples.FullBox("ispe", 0, 0, AvifSamples.UInt32(1)), entries: 1, index: 1) },
                { "an AVIF association list cut short", AvifSamples.WithProperty(AvifSamples.Ispe(1, 1), entries: 9, index: 1) },
                { "an AVIF box longer than the file", ImageSamples.Concat(ftyp, meta, AvifSamples.UInt32(999), ImageSamples.Ascii("mdat")) },
                { "an AVIF box shorter than its header", ImageSamples.Concat(ftyp, meta, AvifSamples.UInt32(4), ImageSamples.Ascii("mdat")) },
                { "an AVIF cut inside a box header", ImageSamples.Concat(ftyp, meta, [0, 0]) },
                { "an AVIF 64-bit size longer than the file", ImageSamples.Concat(ftyp, meta, AvifSamples.UInt32(1), ImageSamples.Ascii("mdat"), [0xFF, 0, 0, 0, 0, 0, 0, 0]) },
                { "an AVIF child box longer than its parent", ImageSamples.Concat(ftyp, AvifSamples.Box("meta", ImageSamples.Concat([0, 0, 0, 0], AvifSamples.UInt32(500), ImageSamples.Ascii("pitm"))), mdat) },
                { "an AVIF meta box too short for its version", ImageSamples.Concat(ftyp, AvifSamples.Box("meta", [0, 0]), mdat) },
                { "an AVIF ftyp box cut inside its brands", ImageSamples.Concat(AvifSamples.UInt32(18), ImageSamples.Ascii("ftypavif"), [0, 0, 0, 0, 0x61, 0x76], meta, mdat) },
                { "an AVIF whose last box is cut", avif[..^1] },
            };
        }
    }
}
