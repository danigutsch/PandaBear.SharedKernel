using System.Buffers.Binary;

namespace PandaBear.UploadSanitizing.Tests.Fonts;

/// <summary>
/// Builds small WOFF2 files: a well-formed header, table directory and compressed-data block of arbitrary bytes. The data is not a
/// decodable font, which the validator does not need; a browser would drop it.
/// </summary>
internal static class Woff2Samples
{
    /// <summary>The <c>head</c> table with the null transform and an original length of 54.</summary>
    internal static readonly byte[] HeadOnly = [0x01, 0x36];

    /// <summary>Transformed <c>glyf</c> and <c>loca</c> tables; <c>glyf</c> has a transform length, <c>loca</c> one of zero.</summary>
    internal static readonly byte[] TransformedGlyfAndLoca = [0x0A, 0x81, 0x00, 0x40, 0x0B, 0x10, 0x00];

    /// <summary>The <c>glyf</c> and <c>loca</c> tables with the null transform (version 3), so neither has a transform length.</summary>
    internal static readonly byte[] UntransformedGlyfAndLoca = [0xCA, 0x81, 0x00, 0xCB, 0x10];

    /// <summary>A transformed <c>hmtx</c> table (version 1) with a transform length.</summary>
    internal static readonly byte[] TransformedHmtx = [0x43, 0x20, 0x10];

    /// <summary>A table with an arbitrary four-byte tag after the flags.</summary>
    internal static readonly byte[] ArbitraryTag = [0x3F, .. "ABCD"u8, 0x08];

    /// <summary>The <c>wOF2</c> signature.</summary>
    internal const uint Signature = 0x774F4632;

    /// <summary>The TrueType flavour.</summary>
    internal const uint TrueType = 0x00010000;

    /// <summary>The offset of the header's <c>length</c> field.</summary>
    internal const int LengthOffset = 8;

    /// <summary>The offset of the header's <c>totalCompressedSize</c> field.</summary>
    internal const int CompressedSizeOffset = 20;

    /// <summary>The offset of the header's <c>metaOffset</c> field.</summary>
    internal const int MetaOffset = 28;

    /// <summary>The offset of the header's <c>privOffset</c> field.</summary>
    internal const int PrivateOffset = 40;

    /// <summary>Builds a file.</summary>
    /// <param name="marker">Fills the data block, so files with different markers differ.</param>
    /// <param name="directory">The table directory entries.</param>
    /// <param name="tables">The declared number of tables.</param>
    /// <param name="flavor">The flavour.</param>
    /// <param name="dataLength">The length of the compressed-data block.</param>
    /// <param name="patch">Changes the bytes after they are built (to damage the header).</param>
    /// <returns>The file.</returns>
    internal static byte[] Font(byte marker = 1, byte[]? directory = null, ushort tables = 1, uint flavor = TrueType, int dataLength = 8, Action<byte[]>? patch = null)
    {
        directory ??= HeadOnly;
        var bytes = new byte[48 + directory.Length + dataLength];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span, Signature);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], flavor);
        BinaryPrimitives.WriteUInt32BigEndian(span[LengthOffset..], (uint)bytes.Length);
        BinaryPrimitives.WriteUInt16BigEndian(span[12..], tables);
        BinaryPrimitives.WriteUInt32BigEndian(span[16..], 1024);
        BinaryPrimitives.WriteUInt32BigEndian(span[CompressedSizeOffset..], (uint)dataLength);
        BinaryPrimitives.WriteUInt16BigEndian(span[24..], 1);
        directory.CopyTo(span[48..]);
        span[(48 + directory.Length)..].Fill(marker);
        patch?.Invoke(bytes);
        return bytes;
    }

    /// <summary>
    /// Builds a file with a metadata block (at 60, 4 bytes) and a private block (at 64, 2 bytes) after 8 bytes of compressed data: both
    /// start on a four-byte boundary and the private block ends the file.
    /// </summary>
    /// <returns>The file.</returns>
    internal static byte[] FontWithMetadataAndPrivateData() => Font(dataLength: 16, patch: file =>
    {
        Write(file, CompressedSizeOffset, 8);
        Write(file, MetaOffset, 60);
        Write(file, MetaOffset + 4, 4);
        Write(file, MetaOffset + 8, 10);
        Write(file, PrivateOffset, 64);
        Write(file, PrivateOffset + 4, 2);
    });

    /// <summary>Builds <see cref="FontWithMetadataAndPrivateData"/> with the offset and length of one block replaced.</summary>
    /// <param name="block">The header offset of the block: <see cref="MetaOffset"/> or <see cref="PrivateOffset"/>.</param>
    /// <param name="offset">The block's offset.</param>
    /// <param name="length">The block's length.</param>
    /// <returns>The file.</returns>
    internal static byte[] WithBlock(int block, uint offset, uint length)
    {
        var file = FontWithMetadataAndPrivateData();
        Write(file, block, offset);
        Write(file, block + 4, length);
        return file;
    }

    /// <summary>Writes a big-endian number into a built file.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="value">The value.</param>
    internal static void Write(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
}
