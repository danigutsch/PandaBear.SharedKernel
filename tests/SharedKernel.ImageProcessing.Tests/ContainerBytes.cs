using System.Buffers.Binary;
using System.Text;

namespace SharedKernel.ImageProcessing.Tests;

/// <summary>
/// Builds hand-crafted PNG and WebP container bytes for header-walk tests.
/// </summary>
internal static class ContainerBytes
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Returns the PNG signature followed by the given chunks.
    /// </summary>
    public static byte[] Png(params byte[][] chunks) => [.. PngSignature, .. chunks.SelectMany(chunk => chunk)];

    /// <summary>
    /// Returns one PNG chunk (big-endian length, type, data, CRC-32 over type and data).
    /// </summary>
    public static byte[] PngChunk(string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        typeBytes.CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32([.. typeBytes, .. data]));

        return chunk;
    }

    /// <summary>
    /// Returns a PNG chunk header that declares <paramref name="declaredLength"/> bytes but carries none.
    /// </summary>
    public static byte[] PngChunkHeader(string type, uint declaredLength)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, declaredLength);
        Encoding.ASCII.GetBytes(type).CopyTo(header, 4);

        return header;
    }

    /// <summary>
    /// Returns an IHDR chunk for a 1x1, 8-bit RGBA image.
    /// </summary>
    public static byte[] Ihdr() => PngChunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0]);

    /// <summary>
    /// Returns an acTL chunk declaring the given frame count and infinite looping.
    /// </summary>
    public static byte[] Actl(uint frameCount)
    {
        var data = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(data, frameCount);

        return PngChunk("acTL", data);
    }

    /// <summary>
    /// Returns a PNG with IHDR, <paramref name="ancillaryCount"/> tEXt chunks, IDAT and IEND, in that order.
    /// </summary>
    public static byte[] PngWithAncillaryChunks(int ancillaryCount)
    {
        var text = PngChunk("tEXt", [0x61, 0x00, 0x62]);
        var chunks = new List<byte[]> { Ihdr() };
        chunks.AddRange(Enumerable.Repeat(text, ancillaryCount));
        chunks.Add(PngChunk("IDAT", [0x78, 0x9C]));
        chunks.Add(PngChunk("IEND", []));

        return Png([.. chunks]);
    }

    /// <summary>
    /// Inserts <paramref name="chunk"/> directly after the IHDR chunk of an encoded PNG.
    /// </summary>
    public static byte[] InsertAfterIhdr(byte[] png, byte[] chunk)
    {
        const int afterIhdr = 8 + 12 + 13;

        return [.. png.AsSpan(0, afterIhdr), .. chunk, .. png.AsSpan(afterIhdr)];
    }

    /// <summary>
    /// Returns a RIFF WEBP container whose size field covers exactly the given chunks.
    /// </summary>
    public static byte[] WebP(params byte[][] chunks)
    {
        var body = chunks.SelectMany(chunk => chunk).ToArray();

        return WebPWithRiffSize((uint)(4 + body.Length), body);
    }

    /// <summary>
    /// Returns a RIFF WEBP container with an explicit RIFF size field.
    /// </summary>
    public static byte[] WebPWithRiffSize(uint riffSize, byte[] body)
    {
        var header = new byte[12];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), riffSize);
        "WEBP"u8.CopyTo(header.AsSpan(8));

        return [.. header, .. body];
    }

    /// <summary>
    /// Returns one RIFF chunk (FourCC, little-endian size, payload, even padding).
    /// </summary>
    public static byte[] WebPChunk(string fourCc, byte[] payload)
    {
        var chunk = new byte[8 + payload.Length + (payload.Length & 1)];
        Encoding.ASCII.GetBytes(fourCc).CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(chunk, 8);

        return chunk;
    }

    /// <summary>
    /// Returns a RIFF chunk header that declares <paramref name="declaredSize"/> bytes but carries none.
    /// </summary>
    public static byte[] WebPChunkHeader(string fourCc, uint declaredSize)
    {
        var header = new byte[8];
        Encoding.ASCII.GetBytes(fourCc).CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), declaredSize);

        return header;
    }

    /// <summary>
    /// Returns a VP8X chunk with the given flags byte and a 1x1 canvas.
    /// </summary>
    public static byte[] Vp8x(byte flags) => WebPChunk("VP8X", [flags, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
