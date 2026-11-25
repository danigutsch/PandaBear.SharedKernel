using System.Buffers.Binary;

namespace SharedKernel.UploadSanitizing.Fonts;

/// <summary>
/// Reads the structure of a WOFF2 file (W3C WOFF File Format 2.0) without decoding its compressed data. Every offset and length is compared
/// with the bytes present in 64-bit arithmetic, so a declared value can neither overflow nor size an allocation; the work is linear in the
/// file size, because the table directory is read once and stops at the end of the file.
/// </summary>
/// <remarks>
/// The variable-length 255UInt16 numbers of the format occur only in the collection directory, which is refused with its flavour, and inside
/// the compressed <c>glyf</c> streams, which are not decoded; the table directory uses UIntBase128 alone.
/// </remarks>
internal static class Woff2Reader
{
    /// <summary>The size of the fixed header.</summary>
    internal const int HeaderSize = 48;

    private const uint Signature = 0x774F4632; // "wOF2"
    private const uint TrueTypeFlavor = 0x00010000;
    private const uint CffFlavor = 0x4F54544F; // "OTTO"
    private const uint AppleTrueTypeFlavor = 0x74727565; // "true"
    private const int ArbitraryTag = 63;
    private const int HmtxIndex = 3;
    private const int GlyfIndex = 10;
    private const int LocaIndex = 11;
    private const int MetaAt = 28;
    private const int PrivateAt = 40;

    // The transform state of glyf and loca: absent, transformed (version 0) or null-transformed (version 3).
    private const int Absent = 0;
    private const int Transformed = 1;
    private const int NullTransformed = 2;

    /// <summary>Gets a value indicating whether the bytes hold a whole header that starts with the <c>wOF2</c> signature.</summary>
    /// <param name="data">The file.</param>
    /// <returns>Whether the file starts like a WOFF2 font.</returns>
    internal static bool HasSignature(ReadOnlySpan<byte> data) =>
        data.Length >= HeaderSize && BinaryPrimitives.ReadUInt32BigEndian(data) == Signature;

    /// <summary>
    /// Gets a value indicating whether a file with the signature has a sound structure: a single-font flavour, a declared length equal to the
    /// file's, a zero reserved field, a well-formed table directory, compressed data inside the file, and metadata and private blocks on
    /// four-byte boundaries inside the file after what precedes them.
    /// </summary>
    /// <param name="data">The file; <see cref="HasSignature"/> is true for it.</param>
    /// <returns>Whether the structure is sound.</returns>
    internal static bool IsSound(ReadOnlySpan<byte> data)
    {
        var flavor = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var length = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(data[12..]);
        var reserved = BinaryPrimitives.ReadUInt16BigEndian(data[14..]);
        var sfntSize = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var compressedSize = BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        if (flavor is not (TrueTypeFlavor or CffFlavor or AppleTrueTypeFlavor)
            || length != (uint)data.Length
            || tableCount == 0
            || reserved != 0
            || sfntSize == 0
            || compressedSize == 0)
        {
            return false;
        }

        var offset = HeaderSize;
        var glyf = Absent;
        var loca = Absent;
        for (var table = 0; table < tableCount; table++)
        {
            if (!ReadTableEntry(data, ref offset, ref glyf, ref loca))
            {
                return false;
            }
        }

        // The glyf and loca tables are both absent, both transformed, or both null-transformed.
        if (glyf != loca)
        {
            return false;
        }

        var dataEnd = (long)offset + compressedSize;
        if (dataEnd > length)
        {
            return false;
        }

        var metadataEnd = Block(data, MetaAt, dataEnd, length, hasOriginalLength: true);
        return metadataEnd >= 0 && Block(data, PrivateAt, metadataEnd, length, hasOriginalLength: false) >= 0;
    }

    // One directory entry: flags (a table index and a transform version), an optional four-byte tag, the original length and, for a
    // transformed table, the transform length.
    private static bool ReadTableEntry(ReadOnlySpan<byte> data, ref int offset, ref int glyf, ref int loca)
    {
        if (offset >= data.Length)
        {
            return false;
        }

        var flags = data[offset++];
        var tagIndex = flags & 0x3F;
        var version = flags >> 6;
        if (tagIndex == ArbitraryTag)
        {
            if (offset > data.Length - 4)
            {
                return false;
            }

            offset += 4;
        }

        if (!ReadBase128(data, ref offset, out _))
        {
            return false;
        }

        bool transformed;
        switch (tagIndex)
        {
            case GlyfIndex or LocaIndex:
                // Version 0 is the glyf/loca transform, version 3 the null transform; 1 and 2 are unknown.
                if (version is 1 or 2)
                {
                    return false;
                }

                transformed = version == 0;
                if (tagIndex == GlyfIndex)
                {
                    glyf = transformed ? Transformed : NullTransformed;
                }
                else
                {
                    loca = transformed ? Transformed : NullTransformed;
                }

                break;
            case HmtxIndex:
                // Version 0 is the null transform, version 1 the hmtx transform.
                if (version > 1)
                {
                    return false;
                }

                transformed = version == 1;
                break;
            default:
                // Every other table has only the null transform, version 0.
                if (version != 0)
                {
                    return false;
                }

                transformed = false;
                break;
        }

        if (!transformed)
        {
            return true;
        }

        // The transform length of a transformed loca table is always zero.
        return ReadBase128(data, ref offset, out var transformLength) && (tagIndex != LocaIndex || transformLength == 0);
    }

    // UIntBase128: at most five bytes, no leading zero byte, and no value above 2^32 - 1.
    private static bool ReadBase128(ReadOnlySpan<byte> data, ref int offset, out uint value)
    {
        value = 0;
        for (var index = 0; index < 5; index++)
        {
            if (offset >= data.Length)
            {
                return false;
            }

            var next = data[offset++];
            if ((index == 0 && next == 0x80) || (value & 0xFE000000) != 0)
            {
                return false;
            }

            value = (value << 7) | (uint)(next & 0x7F);
            if ((next & 0x80) == 0)
            {
                return true;
            }
        }

        return false;
    }

    // The metadata (at 28, with an original length) and private (at 40) blocks: absent with every field zero, or a non-empty block that
    // starts on a four-byte boundary at or after what precedes it and ends inside the file. Returns the end of the block, or of what
    // precedes it when the block is absent, or -1 when the block is not sound.
    private static long Block(ReadOnlySpan<byte> data, int at, long earliest, uint length, bool hasOriginalLength)
    {
        var blockOffset = BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
        var blockLength = BinaryPrimitives.ReadUInt32BigEndian(data[(at + 4)..]);
        if (blockOffset == 0)
        {
            var originalLength = hasOriginalLength ? BinaryPrimitives.ReadUInt32BigEndian(data[(at + 8)..]) : 0;
            return blockLength == 0 && originalLength == 0 ? earliest : -1;
        }

        var end = (long)blockOffset + blockLength;
        return blockOffset >= earliest && blockOffset % 4 == 0 && blockLength > 0 && end <= length ? end : -1;
    }
}
