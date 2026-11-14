using System.Buffers.Binary;

namespace PandaBear.UploadSanitizing.Images;

/// <summary>
/// Compacts a WebP in place without its EXIF and XMP chunks, clears the matching flags of the extended header (VP8X) and writes the new RIFF
/// size. Every other chunk, including the ICCP colour profile, is kept unchanged. The RIFF size must match the file exactly and every chunk
/// must carry its padding byte. The size comes from the extended header, or else from the VP8 or VP8L bitstream header; a simple file holds
/// exactly one bitstream. The largest side of the canvas and of every bitstream is reported, so a decoder that trusts a bitstream over a small
/// canvas is bounded too.
/// </summary>
internal static class WebpSanitizer
{
    private const int HeaderLength = 12;
    private const int ChunkHeaderLength = 8;
    private const int ExtendedHeaderLength = 10;
    private const byte ExifFlag = 0x08;
    private const byte XmpFlag = 0x04;
    private const byte AnimationFlag = 0x02;

    /// <summary>Cleans a buffer that starts with a RIFF header of type WEBP, moving the kept chunks towards its start.</summary>
    /// <param name="data">The file; overwritten with the result.</param>
    /// <param name="width">The width, or zero.</param>
    /// <param name="height">The height, or zero.</param>
    /// <param name="largestSide">The largest width or height of the canvas and of every bitstream.</param>
    /// <param name="animated">
    /// Whether the file is animated: an <c>ANIM</c> or <c>ANMF</c> chunk, or the animation flag of the extended header.
    /// </param>
    /// <returns>The length of the result, or <c>-1</c> when the structure is malformed.</returns>
    internal static int Clean(Span<byte> data, out int width, out int height, out int largestSide, out bool animated)
    {
        width = 0;
        height = 0;
        largestSide = 0;
        animated = false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != (uint)(data.Length - 8))
        {
            return -1;
        }

        var written = HeaderLength;
        var position = HeaderLength;
        var flagsAt = -1;
        var hasImage = false;
        while (position < data.Length)
        {
            if (data.Length - position < ChunkHeaderLength)
            {
                return -1;
            }

            var fourCc = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            var padded = (long)size + (size & 1);
            if (padded > data.Length - position - ChunkHeaderLength)
            {
                return -1;
            }

            var payload = data.Slice(position + ChunkHeaderLength, (int)size);
            var first = position == HeaderLength;
            if (first && fourCc is not (FourCc.Vp8X or FourCc.Vp8 or FourCc.Vp8L))
            {
                return -1;
            }

            switch (fourCc)
            {
                case FourCc.Vp8X:
                    if (!first || size < ExtendedHeaderLength)
                    {
                        return -1;
                    }

                    flagsAt = written + ChunkHeaderLength;
                    animated |= (payload[0] & AnimationFlag) != 0;
                    width = 1 + ReadUInt24(payload[4..]);
                    height = 1 + ReadUInt24(payload[7..]);
                    largestSide = Math.Max(largestSide, Math.Max(width, height));
                    break;
                case FourCc.Vp8 or FourCc.Vp8L:
                    if (!TryReadBitstreamSize(fourCc, payload, out var streamWidth, out var streamHeight) || (flagsAt < 0 && hasImage))
                    {
                        return -1;
                    }

                    largestSide = Math.Max(largestSide, Math.Max(streamWidth, streamHeight));

                    // An extended file takes its size from the canvas; a simple file from its only bitstream.
                    if (flagsAt < 0)
                    {
                        (width, height) = (streamWidth, streamHeight);
                    }

                    hasImage = true;
                    break;
                case FourCc.Anmf:
                    hasImage = true;
                    animated = true;
                    break;
                case FourCc.Anim:
                    animated = true;
                    break;
                default:
                    break;
            }

            var chunkLength = ChunkHeaderLength + (int)padded;
            if (fourCc is not (FourCc.Exif or FourCc.Xmp))
            {
                data.Slice(position, chunkLength).CopyTo(data[written..]);
                written += chunkLength;
            }

            position += chunkLength;
        }

        if (!hasImage)
        {
            return -1;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(data[4..], (uint)(written - 8));
        if (flagsAt >= 0)
        {
            data[flagsAt] &= unchecked((byte)~(ExifFlag | XmpFlag));
        }

        return written;
    }

    private static bool TryReadBitstreamSize(uint fourCc, ReadOnlySpan<byte> payload, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (fourCc == FourCc.Vp8L)
        {
            // A lossless bitstream starts with the signature 0x2F, then 14 bits of width - 1 and 14 bits of height - 1.
            if (payload.Length < 5 || payload[0] != 0x2F)
            {
                return false;
            }

            var bits = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
            return true;
        }

        // A lossy key frame has a three-byte frame tag, the start code 9D 01 2A, then 14 bits of width and of height.
        if (payload.Length < 10 || payload[3] != 0x9D || payload[4] != 0x01 || payload[5] != 0x2A)
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]) & 0x3FFF;
        height = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]) & 0x3FFF;
        return true;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    /// <summary>The chunk ids the sanitizer reads, as big-endian integers of their four ASCII characters.</summary>
    private static class FourCc
    {
        internal const uint Vp8X = 0x56503858; // "VP8X"
        internal const uint Vp8 = 0x56503820; // "VP8 "
        internal const uint Vp8L = 0x5650384C; // "VP8L"
        internal const uint Anmf = 0x414E4D46; // "ANMF"
        internal const uint Anim = 0x414E494D; // "ANIM"
        internal const uint Exif = 0x45584946; // "EXIF"
        internal const uint Xmp = 0x584D5020; // "XMP "
    }
}
