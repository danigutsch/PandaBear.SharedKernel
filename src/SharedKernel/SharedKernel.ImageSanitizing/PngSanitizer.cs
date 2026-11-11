using System.Buffers.Binary;

namespace PandaBear.ImageSanitizing;

/// <summary>
/// Compacts a PNG in place without its eXIf, tEXt, iTXt, zTXt and tIME chunks and without bytes after the IEND chunk. Every other chunk,
/// including the iCCP colour profile, is kept unchanged with its CRC. The size comes from the IHDR chunk, which must come first and once.
/// </summary>
internal static class PngSanitizer
{
    private const int SignatureLength = 8;
    private const int ChunkOverhead = 12;
    private const int HeaderDataLength = 13;

    /// <summary>Gets the eight-byte PNG signature.</summary>
    internal static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Cleans a buffer that starts with the PNG signature, moving the kept chunks towards its start.</summary>
    /// <param name="data">The file; overwritten with the result.</param>
    /// <param name="width">The width from the header chunk, or zero.</param>
    /// <param name="height">The height from the header chunk, or zero.</param>
    /// <returns>The length of the result, or <c>-1</c> when the structure is malformed.</returns>
    internal static int Clean(Span<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var written = SignatureLength;
        var position = SignatureLength;
        var hasImageData = false;
        while (data.Length - position >= ChunkOverhead)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            if (length > (uint)(data.Length - position - ChunkOverhead))
            {
                return -1;
            }

            var type = data.Slice(position + 4, 4);
            if (!IsChunkType(type))
            {
                return -1;
            }

            var isHeader = type.SequenceEqual("IHDR"u8);
            if (isHeader != (position == SignatureLength))
            {
                return -1;
            }

            if (isHeader)
            {
                if (length != HeaderDataLength)
                {
                    return -1;
                }

                width = BinaryPrimitives.ReadInt32BigEndian(data[(position + 8)..]);
                height = BinaryPrimitives.ReadInt32BigEndian(data[(position + 12)..]);
            }

            var isEnd = type.SequenceEqual("IEND"u8);
            hasImageData |= type.SequenceEqual("IDAT"u8);
            var chunkLength = ChunkOverhead + (int)length;
            if (!IsMetadata(type))
            {
                data.Slice(position, chunkLength).CopyTo(data[written..]);
                written += chunkLength;
            }

            position += chunkLength;
            if (isEnd)
            {
                return hasImageData ? written : -1;
            }
        }

        return -1;
    }

    private static bool IsChunkType(ReadOnlySpan<byte> type)
    {
        foreach (var character in type)
        {
            if (!char.IsAsciiLetter((char)character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMetadata(ReadOnlySpan<byte> type) =>
        type.SequenceEqual("eXIf"u8)
        || type.SequenceEqual("tEXt"u8)
        || type.SequenceEqual("iTXt"u8)
        || type.SequenceEqual("zTXt"u8)
        || type.SequenceEqual("tIME"u8);
}
