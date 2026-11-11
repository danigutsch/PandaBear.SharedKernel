using System.Buffers.Binary;
using System.Text;

namespace PandaBear.ImageSanitizing.Tests;

/// <summary>
/// Image files for the sanitizer tests. The PNG and lossless WebP are real 1x1 images; the JPEG and lossy WebP are structurally valid (every
/// part the sanitizer reads) with made-up image data. The "with metadata" variants carry GPS and camera text in every metadata block the
/// sanitizer must remove, and a colour profile it must keep.
/// </summary>
internal static class ImageSamples
{
    /// <summary>The text planted in every metadata block, so a test can prove it is gone.</summary>
    internal const string Secret = "GPS 38.7223N 9.1393W Canon EOS";

    /// <summary>The text of every colour profile, so a test can prove it is kept.</summary>
    internal const string Profile = "ICC-sRGB-profile";

    /// <summary>The offset just after the PNG signature and header chunk.</summary>
    internal const int PngAfterHeader = 8 + 25;

    /// <summary>A real 1x1 PNG.</summary>
    /// <returns>The file.</returns>
    internal static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>The 1x1 PNG with an iCCP colour profile after the header.</summary>
    /// <returns>The file.</returns>
    internal static byte[] PngWithProfile()
    {
        var png = Png();
        return Concat(png[..PngAfterHeader], PngChunk("iCCP", Ascii("sRGB\0\0" + Profile)), png[PngAfterHeader..]);
    }

    /// <summary>The PNG with a profile, plus eXIf, tEXt, iTXt, zTXt and tIME chunks after the header and data after the end chunk.</summary>
    /// <returns>The file.</returns>
    internal static byte[] PngWithMetadata()
    {
        var png = PngWithProfile();
        var metadata = Concat(
            PngChunk("eXIf", Ascii(Secret)),
            PngChunk("tEXt", Ascii("Author\0" + Secret)),
            PngChunk("iTXt", Ascii("GPS\0\0\0\0\0" + Secret)),
            PngChunk("zTXt", Ascii("Comment\0\0" + Secret)),
            PngChunk("tIME", [0x07, 0xEA, 1, 2, 3, 4, 5]));
        return Concat(png[..PngAfterHeader], metadata, png[PngAfterHeader..], Ascii(Secret));
    }

    /// <summary>A real 1x1 lossless WebP (a <c>VP8L</c> chunk).</summary>
    /// <returns>The file.</returns>
    internal static byte[] Webp() => Convert.FromBase64String("UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==");

    /// <summary>A simple-format lossy WebP whose <c>VP8 </c> key frame header declares the size.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The file.</returns>
    internal static byte[] LossyWebp(int width, int height)
    {
        var frame = new byte[10];
        frame[3] = 0x9D;
        frame[4] = 0x01;
        frame[5] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), (ushort)height);
        return Riff(Concat(Ascii("WEBP"), RiffChunk("VP8 ", frame)));
    }

    /// <summary>The 1x1 WebP in an extended container (<c>VP8X</c>) that declares and carries ICC, EXIF and XMP chunks.</summary>
    /// <param name="width">The canvas width.</param>
    /// <param name="height">The canvas height.</param>
    /// <returns>The file.</returns>
    internal static byte[] WebpWithMetadata(int width = 1, int height = 1)
    {
        var vp8l = Webp()[12..];
        var header = new byte[10];
        header[0] = 0x20 | 0x10 | 0x08 | 0x04;
        WriteUInt24(header, 4, width - 1);
        WriteUInt24(header, 7, height - 1);
        var body = Concat(
            Ascii("WEBP"),
            RiffChunk("VP8X", header),
            RiffChunk("ICCP", Ascii(Profile)),
            vp8l,
            RiffChunk("EXIF", Ascii(Secret + "!")),
            RiffChunk("XMP ", Ascii("<x:xmpmeta>" + Secret + "</x:xmpmeta>")));
        return Riff(body);
    }

    /// <summary>The 1x1 PNG made an animated PNG by an <c>acTL</c> chunk before its image data.</summary>
    /// <returns>The file.</returns>
    internal static byte[] AnimatedPng()
    {
        var png = Png();
        return Concat(png[..PngAfterHeader], PngChunk("acTL", [0, 0, 0, 1, 0, 0, 0, 0]), png[PngAfterHeader..]);
    }

    /// <summary>The 1x1 WebP in an extended container whose header may set the animation flag, with an optional extra chunk.</summary>
    /// <param name="animationFlag">Whether the extended header sets the animation flag.</param>
    /// <param name="extraChunk">The id of a chunk placed before the bitstream, or <see langword="null"/> for none.</param>
    /// <returns>The file.</returns>
    internal static byte[] ExtendedWebp(bool animationFlag, string? extraChunk)
    {
        var header = new byte[10];
        header[0] = animationFlag ? (byte)0x02 : (byte)0;
        return Riff(Concat(
            Ascii("WEBP"),
            RiffChunk("VP8X", header),
            extraChunk is null ? [] : RiffChunk(extraChunk, new byte[16]),
            Webp()[12..]));
    }

    /// <summary>A JPEG without metadata: JFIF, an ICC profile, a quantization table, the frame header, a scan and the end marker.</summary>
    /// <param name="width">The width in the frame header.</param>
    /// <param name="height">The height in the frame header.</param>
    /// <returns>The file.</returns>
    internal static byte[] Jpeg(int width = 640, int height = 480) => Concat(
        [0xFF, 0xD8],
        Jfif(),
        IccSegment(),
        Quantization(),
        Frame(width, height),
        Scan(),
        [0xFF, 0xD9]);

    /// <summary>The same JPEG with Exif (APP1), XMP (APP1), Photoshop (APP13) and comment (COM) segments and data after the end marker.</summary>
    /// <param name="width">The width in the frame header.</param>
    /// <param name="height">The height in the frame header.</param>
    /// <returns>The file.</returns>
    internal static byte[] JpegWithMetadata(int width = 640, int height = 480) => Concat(
        [0xFF, 0xD8],
        Jfif(),
        JpegSegment(0xE1, Ascii("Exif\0\0" + Secret)),
        JpegSegment(0xE1, Ascii("http://ns.adobe.com/xap/1.0/\0" + Secret)),
        IccSegment(),
        JpegSegment(0xED, Ascii("Photoshop 3.0\0" + Secret)),
        JpegSegment(0xFE, Ascii(Secret)),
        Quantization(),
        Frame(width, height),
        Scan(),
        [0xFF, 0xD9],
        Ascii(Secret));

    /// <summary>A progressive-style JPEG: two scans with a table segment and a comment between them.</summary>
    /// <returns>The file.</returns>
    internal static byte[] JpegWithTwoScans() => Concat(
        [0xFF, 0xD8],
        Jfif(),
        Quantization(),
        JpegSegment(0xC2, [8, 0, 10, 0, 20, 1, 1, 0x11, 0]),
        Scan(),
        Quantization(),
        JpegSegment(0xFE, Ascii(Secret)),
        Scan(),
        [0xFF, 0xD9]);

    /// <summary>The same JPEG as <see cref="JpegWithTwoScans"/> without the comment.</summary>
    /// <returns>The file.</returns>
    internal static byte[] JpegWithTwoScansClean() => Concat(
        [0xFF, 0xD8],
        Jfif(),
        Quantization(),
        JpegSegment(0xC2, [8, 0, 10, 0, 20, 1, 1, 0x11, 0]),
        Scan(),
        Quantization(),
        Scan(),
        [0xFF, 0xD9]);

    /// <summary>Wraps data in a JPEG marker segment.</summary>
    /// <param name="marker">The marker byte after 0xFF.</param>
    /// <param name="data">The segment data.</param>
    /// <returns>The segment.</returns>
    internal static byte[] JpegSegment(byte marker, byte[] data)
    {
        var length = data.Length + 2;
        return Concat([0xFF, marker, (byte)(length >> 8), (byte)length], data);
    }

    /// <summary>A frame header segment.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The segment.</returns>
    internal static byte[] Frame(int width, int height) =>
        JpegSegment(0xC0, [8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0]);

    /// <summary>A start-of-scan segment followed by entropy-coded data with a stuffed byte and a restart marker.</summary>
    /// <returns>The scan.</returns>
    internal static byte[] Scan() => Concat(JpegSegment(0xDA, [1, 1, 0, 0, 63, 0]), [0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56]);

    /// <summary>Wraps data in a PNG chunk with a zero CRC (the sanitizer copies CRCs without checking them).</summary>
    /// <param name="type">The four-letter type.</param>
    /// <param name="data">The chunk data.</param>
    /// <returns>The chunk.</returns>
    internal static byte[] PngChunk(string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        return Concat(length, Ascii(type), data, [0, 0, 0, 0]);
    }

    /// <summary>Wraps data in a RIFF chunk, padded to an even length.</summary>
    /// <param name="fourCc">The chunk id.</param>
    /// <param name="data">The chunk data.</param>
    /// <returns>The chunk.</returns>
    internal static byte[] RiffChunk(string fourCc, byte[] data) =>
        Concat(Ascii(fourCc), BitConverter.GetBytes(data.Length), data, data.Length % 2 == 1 ? [0] : []);

    /// <summary>Wraps a body that starts with <c>WEBP</c> in a RIFF header with the matching size.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The file.</returns>
    internal static byte[] Riff(byte[] body) => Concat(Ascii("RIFF"), BitConverter.GetBytes(body.Length), body);

    /// <summary>Copies a file with one byte changed.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="index">The position.</param>
    /// <param name="value">The new byte.</param>
    /// <returns>The changed copy.</returns>
    internal static byte[] Corrupt(byte[] bytes, int index, byte value)
    {
        var copy = bytes.ToArray();
        copy[index] = value;
        return copy;
    }

    /// <summary>Copies a file with four bytes overwritten by a big-endian value.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="index">The position.</param>
    /// <param name="value">The value.</param>
    /// <returns>The changed copy.</returns>
    internal static byte[] WithUInt32BigEndian(byte[] bytes, int index, uint value)
    {
        var copy = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(index), value);
        return copy;
    }

    /// <summary>Copies a file with four bytes overwritten by a little-endian value.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="index">The position.</param>
    /// <param name="value">The value.</param>
    /// <returns>The changed copy.</returns>
    internal static byte[] WithUInt32LittleEndian(byte[] bytes, int index, uint value)
    {
        var copy = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(index), value);
        return copy;
    }

    /// <summary>Joins byte arrays.</summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The joined bytes.</returns>
    internal static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>Encodes text as ASCII bytes.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>Whether the planted metadata text occurs anywhere in the bytes.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns><see langword="true"/> when the secret is still there.</returns>
    internal static bool ContainsSecret(ReadOnlySpan<byte> bytes) => bytes.IndexOf("GPS"u8) >= 0;

    /// <summary>Whether the colour profile text occurs in the bytes.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns><see langword="true"/> when the profile is there.</returns>
    internal static bool ContainsProfile(ReadOnlySpan<byte> bytes) => bytes.IndexOf("ICC-sRGB-profile"u8) >= 0;

    private static byte[] Jfif() => JpegSegment(0xE0, [.. Ascii("JFIF\0"), 1, 1, 0, 0, 1, 0, 1, 0, 0]);

    private static byte[] IccSegment() => JpegSegment(0xE2, Ascii("ICC_PROFILE\0\x01\x01" + Profile));

    private static byte[] Quantization() => JpegSegment(0xDB, [0, .. Enumerable.Repeat((byte)1, 64)]);

    private static void WriteUInt24(byte[] target, int offset, int value)
    {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)(value >> 16);
    }
}
