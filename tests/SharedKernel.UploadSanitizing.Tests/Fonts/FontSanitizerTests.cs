using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Fonts;

/// <summary>
/// Tests the WOFF2 checks of <see cref="FontSanitizer"/>: a sound font is returned unchanged with its format, extension and media type, and
/// every structural fault of the header, the table directory and the data blocks is refused with one message.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class FontSanitizerTests
{
    [Fact]
    public void A_sound_woff2_is_returned_unchanged_with_its_format_extension_and_media_type()
    {
        // Arrange
        var file = Woff2Samples.Font();

        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        var font = result.ShouldBeSuccess();
        font.Bytes.ToArray().ShouldBe(file);
        font.Format.ShouldBe(FontFormat.Woff2);
        font.FileExtension.ShouldBe(".woff2");
        font.ContentType.ShouldBe("font/woff2");
    }

    /// <summary>Gets sound fonts with every directory and block shape the validator reads.</summary>
    public static TheoryData<string, byte[]> Accepted => new()
    {
        { "one null-transformed table", Woff2Samples.Font() },
        { "transformed glyf and loca", Woff2Samples.Font(directory: Woff2Samples.TransformedGlyfAndLoca, tables: 2) },
        { "null-transformed glyf and loca", Woff2Samples.Font(directory: Woff2Samples.UntransformedGlyfAndLoca, tables: 2) },
        { "a transformed hmtx", Woff2Samples.Font(directory: Woff2Samples.TransformedHmtx) },
        { "an arbitrary tag", Woff2Samples.Font(directory: Woff2Samples.ArbitraryTag) },
        { "the largest five-byte original length", Woff2Samples.Font(directory: [0x01, 0x8F, 0xFF, 0xFF, 0xFF, 0x7F]) },
        { "the CFF flavour", Woff2Samples.Font(flavor: 0x4F54544F) },
        { "the Apple TrueType flavour", Woff2Samples.Font(flavor: 0x74727565) },
        { "metadata and private blocks", Woff2Samples.FontWithMetadataAndPrivateData() },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void A_sound_woff2_is_accepted(string kind, byte[] file)
    {
        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        result.IsSuccess.ShouldBeTrue(kind);
    }

    /// <summary>Gets files that do not start like a WOFF2 font.</summary>
    public static TheoryData<string, byte[]> NotFonts => new()
    {
        { "an empty file", [] },
        { "a PNG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[60]] },
        { "a WOFF 1 font", [.. "wOFF"u8, .. new byte[60]] },
        { "a signature alone", [.. "wOF2"u8] },
        { "a header one byte short", Woff2Samples.Font()[..47] },
    };

    [Theory]
    [MemberData(nameof(NotFonts))]
    public void A_file_that_does_not_start_like_a_woff2_is_not_a_font(string kind, byte[] file)
    {
        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        result.IsFailure.ShouldBeTrue(kind);
        result.ShouldBeRejected(FontSanitizer.NotAFontMessage);
    }

    /// <summary>Gets WOFF2 files with a damaged or unsupported structure.</summary>
    public static TheoryData<string, byte[]> Malformed => new()
    {
        { "a wrong length", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.LengthOffset, 9999)) },
        { "a font collection", Woff2Samples.Font(flavor: 0x74746366) },
        { "an unknown flavour", Woff2Samples.Font(flavor: 0x12345678) },
        { "no tables", Woff2Samples.Font(tables: 0) },
        { "a reserved field", Woff2Samples.Font(patch: bytes => bytes[15] = 1) },
        { "a zero total sfnt size", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, 16, 0)) },
        { "a zero compressed size", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, 0)) },
        { "a directory past the end", Woff2Samples.Font(tables: 9) },
        { "an arbitrary tag cut short", Woff2Samples.Font(directory: [0x3F, 0x41, 0x42], dataLength: 0) },
        { "a number with a leading zero", Woff2Samples.Font(directory: [0x01, 0x80, 0x36]) },
        { "a number longer than five bytes", Woff2Samples.Font(directory: [0x01, 0x81, 0x81, 0x81, 0x81, 0x81, 0x01]) },
        { "a number above 2^32 - 1", Woff2Samples.Font(directory: [0x01, 0x90, 0x80, 0x80, 0x80, 0x00]) },
        { "a number cut short", Woff2Samples.Font(directory: [0x01, 0x81], dataLength: 0) },
        { "a transformed table without its length", Woff2Samples.Font(directory: [0x0A, 0x36], dataLength: 0) },
        { "a transformed loca with a length", Woff2Samples.Font(directory: [0x0A, 0x81, 0x00, 0x40, 0x0B, 0x10, 0x04], tables: 2) },
        { "a transformed glyf with a null-transformed loca", Woff2Samples.Font(directory: [0x0A, 0x81, 0x00, 0x40, 0xCB, 0x10], tables: 2) },
        { "a null-transformed glyf with a transformed loca", Woff2Samples.Font(directory: [0xCA, 0x81, 0x00, 0x0B, 0x10, 0x00], tables: 2) },
        { "a glyf without a loca", Woff2Samples.Font(directory: [0xCA, 0x81, 0x00]) },
        { "an unknown glyf transform", Woff2Samples.Font(directory: [0x4A, 0x81, 0x00, 0x40, 0x0B, 0x10, 0x00], tables: 2) },
        { "an unknown hmtx transform", Woff2Samples.Font(directory: [0x83, 0x20, 0x10]) },
        { "a transform of a table that has none", Woff2Samples.Font(directory: [0x41, 0x36, 0x10]) },
        { "compressed data past the end", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, 500)) },
        { "metadata past the end", Woff2Samples.Font(patch: bytes => { Woff2Samples.Write(bytes, Woff2Samples.MetaOffset, 60); Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 4, 500); }) },
        { "a metadata length without an offset", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 4, 4)) },
        { "a metadata original length without a block", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 8, 4)) },
        { "a metadata offset without a length", Woff2Samples.Font(dataLength: 12, patch: bytes => { Woff2Samples.Write(bytes, Woff2Samples.CompressedSizeOffset, 4); Woff2Samples.Write(bytes, Woff2Samples.MetaOffset, 56); }) },
        { "metadata inside the font data", Woff2Samples.Font(patch: bytes => { Woff2Samples.Write(bytes, Woff2Samples.MetaOffset, 52); Woff2Samples.Write(bytes, Woff2Samples.MetaOffset + 4, 4); }) },
        { "metadata off a four-byte boundary", Woff2Samples.WithBlock(Woff2Samples.MetaOffset, 59, 4) },
        { "private data inside the font data", Woff2Samples.Font(patch: bytes => { Woff2Samples.Write(bytes, Woff2Samples.PrivateOffset, 48); Woff2Samples.Write(bytes, Woff2Samples.PrivateOffset + 4, 2); }) },
        { "private data inside the metadata", Woff2Samples.WithBlock(Woff2Samples.PrivateOffset, 60, 2) },
        { "private data off a four-byte boundary", Woff2Samples.WithBlock(Woff2Samples.PrivateOffset, 65, 1) },
        { "a private length without an offset", Woff2Samples.Font(patch: bytes => Woff2Samples.Write(bytes, Woff2Samples.PrivateOffset + 4, 2)) },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void A_woff2_with_a_damaged_or_unsupported_structure_is_malformed(string kind, byte[] file)
    {
        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        result.IsFailure.ShouldBeTrue(kind);
        result.ShouldBeRejected(FontSanitizer.MalformedMessage);
    }

    [Fact]
    public void A_file_larger_than_the_limit_is_too_large_and_one_of_exactly_the_limit_is_accepted()
    {
        // Arrange
        var file = Woff2Samples.Font();
        var exact = FontSanitizerOptions.Default with
        {
            MaxBytes = file.Length
        };
        var smaller = FontSanitizerOptions.Default with
        {
            MaxBytes = file.Length - 1
        };

        // Act
        var atLimit = FontSanitizer.Sanitize(file, exact);
        var overLimit = FontSanitizer.Sanitize(file, smaller);
        var overLimitInPlace = FontSanitizer.SanitizeInPlace(file, smaller);

        // Assert
        atLimit.IsSuccess.ShouldBeTrue();
        overLimit.ShouldBeRejected(FontSanitizer.TooLargeMessage);
        overLimitInPlace.ShouldBeRejected(FontSanitizer.TooLargeMessage);
    }

    [Fact]
    public void A_font_over_two_mebibytes_is_too_large_by_default()
    {
        // Arrange
        var file = Woff2Samples.Font(dataLength: 2 * 1024 * 1024);

        // Act
        var result = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default);

        // Assert
        result.ShouldBeRejected(FontSanitizer.TooLargeMessage);
    }

    [Fact]
    public void A_rejection_is_reported_on_the_configured_field()
    {
        // Arrange
        var options = FontSanitizerOptions.Default with
        {
            Field = "font"
        };

        // Act
        var result = FontSanitizer.Sanitize("wOFF"u8, options);

        // Assert
        result.ShouldBeRejected(FontSanitizer.NotAFontMessage, "font");
    }

    [Fact]
    public void Sanitizing_in_place_returns_a_slice_of_the_callers_buffer_without_a_copy()
    {
        // Arrange
        var file = Woff2Samples.Font();
        var buffer = new Memory<byte>(file);

        // Act
        var result = FontSanitizer.SanitizeInPlace(buffer, FontSanitizerOptions.Default);

        // Assert
        var font = result.ShouldBeSuccess();
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(font.Bytes, out var segment).ShouldBeTrue();
        segment.Array.ShouldBeSameAs(file);
        segment.Count.ShouldBe(file.Length);
    }

    [Fact]
    public void Sanitizing_a_span_returns_a_copy_that_does_not_follow_later_changes_of_the_input()
    {
        // Arrange
        var file = Woff2Samples.Font();
        var original = file.ToArray();

        // Act
        var font = FontSanitizer.Sanitize(file, FontSanitizerOptions.Default).ShouldBeSuccess();
        file[^1] = 0xEE;

        // Assert
        font.Bytes.ToArray().ShouldBe(original);
    }
}
