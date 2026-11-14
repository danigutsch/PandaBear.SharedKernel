using System.Buffers.Binary;

namespace PandaBear.UploadSanitizing.Tests.Images;

/// <summary>
/// AVIF files for the sanitizer tests: ISOBMFF boxes that carry every part the sanitizer reads (the <c>ftyp</c> brands, the primary item
/// in <c>pitm</c>, the <c>ispe</c> properties in <c>ipco</c> and their association in <c>ipma</c>) with made-up coded image data.
/// </summary>
internal static class AvifSamples
{
    /// <summary>An AVIF whose primary item has the given size, with an Exif item that the sanitizer cannot remove.</summary>
    /// <param name="width">The width of the primary item.</param>
    /// <param name="height">The height of the primary item.</param>
    /// <param name="majorBrand">The major brand.</param>
    /// <param name="compatibleBrands">The compatible brands.</param>
    /// <returns>The file.</returns>
    internal static byte[] Avif(int width = 64, int height = 48, string majorBrand = "avif", params string[] compatibleBrands) =>
        File(Ftyp(majorBrand, compatibleBrands.Length == 0 ? ["avif", "mif1", "miaf"] : compatibleBrands), Meta(Pitm(1), Iprp(width, height)), Mdat());

    /// <summary>An animated AVIF: the <c>avis</c> brand, a still primary item and a <c>moov</c> box.</summary>
    /// <returns>The file.</returns>
    internal static byte[] AnimatedAvif() =>
        File(Ftyp("avis", ["avis", "avif", "msf1", "miaf"]), Meta(Pitm(1), Iprp(64, 48)), Box("moov", []), Mdat());

    /// <summary>A still AVIF that carries a <c>moov</c> box without the <c>avis</c> brand.</summary>
    /// <returns>The file.</returns>
    internal static byte[] AvifWithMovie() =>
        File(Ftyp("avif", ["avif", "mif1"]), Meta(Pitm(1), Iprp(64, 48)), Box("moov", []), Mdat());

    /// <summary>
    /// An AVIF with two items: the primary item 1 of the given size and an auxiliary item 2 (an alpha plane or thumbnail) of another size.
    /// </summary>
    /// <param name="primary">The size of item 1.</param>
    /// <param name="other">The size of item 2.</param>
    /// <returns>The file.</returns>
    internal static byte[] AvifWithTwoItems((int Width, int Height) primary, (int Width, int Height) other)
    {
        var ipco = Box("ipco", Concat(Ispe(other.Width, other.Height), Ispe(primary.Width, primary.Height)));
        var ipma = FullBox("ipma", 0, 0, Concat(UInt32(2), UInt16(2), [1, 0x01], UInt16(1), [1, 0x02]));
        return File(Ftyp("avif", ["avif", "mif1"]), Meta(Pitm(1), Box("iprp", Concat(ipco, ipma))), Mdat());
    }

    /// <summary>A still AVIF whose only property is the given box, with an association list of the given entry count for item 1.</summary>
    /// <param name="property">The property box.</param>
    /// <param name="entries">The declared number of association entries; one is present.</param>
    /// <param name="index">The 1-based property index that item 1 is associated with.</param>
    /// <returns>The file.</returns>
    internal static byte[] WithProperty(byte[] property, uint entries, byte index)
    {
        var ipma = FullBox("ipma", 0, 0, Concat(UInt32(entries), UInt16(1), [1, index]));
        var iprp = Box("iprp", Concat(Box("ipco", property), ipma));
        return File(Ftyp("avif", "avif", "mif1"), Meta(Pitm(1), iprp), Mdat());
    }

    /// <summary>The ftyp box.</summary>
    /// <param name="majorBrand">The major brand.</param>
    /// <param name="compatibleBrands">The compatible brands.</param>
    /// <returns>The box.</returns>
    internal static byte[] Ftyp(string majorBrand, params string[] compatibleBrands) =>
        Box("ftyp", Concat([[.. ImageSamples.Ascii(majorBrand), 0, 0, 0, 0], .. compatibleBrands.Select(ImageSamples.Ascii)]));

    /// <summary>A <c>meta</c> full box with a <c>pict</c> handler and the given children.</summary>
    /// <param name="children">The boxes after the handler.</param>
    /// <returns>The box.</returns>
    internal static byte[] Meta(params byte[][] children) =>
        FullBox("meta", 0, 0, Concat([FullBox("hdlr", 0, 0, Concat(UInt32(0), ImageSamples.Ascii("pict"), new byte[12], [0])), .. children]));

    /// <summary>A <c>pitm</c> box naming the primary item.</summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>The box.</returns>
    internal static byte[] Pitm(ushort itemId) => FullBox("pitm", 0, 0, UInt16(itemId));

    /// <summary>An <c>iprp</c> box with one <c>ispe</c> property associated with item 1.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The box.</returns>
    internal static byte[] Iprp(int width, int height) =>
        Box("iprp", Concat(Box("ipco", Ispe(width, height)), FullBox("ipma", 0, 0, Concat(UInt32(1), UInt16(1), [1, 0x01]))));

    /// <summary>An <c>ispe</c> property.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The box.</returns>
    internal static byte[] Ispe(int width, int height) => FullBox("ispe", 0, 0, Concat(UInt32((uint)width), UInt32((uint)height)));

    /// <summary>An <c>mdat</c> box with made-up coded data and an Exif payload carrying the planted secret.</summary>
    /// <returns>The box.</returns>
    internal static byte[] Mdat() => Box("mdat", Concat([0x12, 0x00, 0x0A, 0x0A], ImageSamples.Ascii("Exif\0\0" + ImageSamples.Secret)));

    /// <summary>Wraps a payload in a box with a 32-bit size.</summary>
    /// <param name="type">The four-character type.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The box.</returns>
    internal static byte[] Box(string type, byte[] payload) => Concat(UInt32((uint)(payload.Length + 8)), ImageSamples.Ascii(type), payload);

    /// <summary>Wraps a payload in a full box: a box whose payload starts with a version byte and 24 bits of flags.</summary>
    /// <param name="type">The four-character type.</param>
    /// <param name="version">The version.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="payload">The payload after the version and flags.</param>
    /// <returns>The box.</returns>
    internal static byte[] FullBox(string type, byte version, int flags, byte[] payload) =>
        Box(type, Concat([version, (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags], payload));

    /// <summary>Joins top-level boxes into a file.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <returns>The file.</returns>
    internal static byte[] File(params byte[][] boxes) => Concat(boxes);

    /// <summary>A big-endian 32-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    /// <summary>A big-endian 16-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] UInt16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => ImageSamples.Concat(parts);
}
