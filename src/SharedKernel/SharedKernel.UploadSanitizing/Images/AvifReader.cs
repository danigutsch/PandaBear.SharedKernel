using System.Buffers.Binary;

namespace PandaBear.UploadSanitizing.Images;

/// <summary>
/// Checks the ISOBMFF box structure of an AVIF without changing it and reads its size and whether it is animated. Every box must fit its
/// parent exactly; <c>ftyp</c> must come first and carry the <c>avif</c> or <c>avis</c> brand; exactly one <c>meta</c> box must name a
/// primary item (<c>pitm</c>) whose image spatial extents property (<c>ispe</c>) is associated with it in <c>ipma</c>. Metadata items (Exif,
/// XMP) are not removed: that needs the item locations (<c>iloc</c>) rewritten, which this reader does not do.
/// </summary>
internal static class AvifReader
{
    private const int BoxHeaderLength = 8;
    private const int LargeBoxHeaderLength = 16;
    private const int FullBoxHeaderLength = 4;
    private const int FtypFixedLength = 8;
    private const int SpatialExtentsLength = 8;

    /// <summary>Whether the leading bytes are an <c>ftyp</c> box that lists the <c>avif</c> or <c>avis</c> brand.</summary>
    /// <param name="data">The file.</param>
    /// <returns><see langword="true"/> when the file claims to be an AVIF.</returns>
    internal static bool IsAvif(ReadOnlySpan<byte> data)
    {
        if (data.Length < BoxHeaderLength + FtypFixedLength || BinaryPrimitives.ReadUInt32BigEndian(data[4..]) != FourCc.Ftyp)
        {
            return false;
        }

        var size = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(data), (uint)data.Length);
        var brands = data[BoxHeaderLength..Math.Max(size, BoxHeaderLength)];
        for (var position = 0; brands.Length - position >= 4; position += position == 0 ? 8 : 4)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(brands[position..]) is FourCc.Avif or FourCc.Avis)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the file and reads the size of its primary item and the largest side of any item.</summary>
    /// <param name="data">The file, starting with its <c>ftyp</c> box.</param>
    /// <param name="width">The width of the primary item, or zero.</param>
    /// <param name="height">The height of the primary item, or zero.</param>
    /// <param name="largestSide">The largest width or height of any <c>ispe</c> property.</param>
    /// <param name="animated">Whether the file is an image sequence: the <c>avis</c> brand or a <c>moov</c> box.</param>
    /// <returns><see langword="false"/> when the structure is malformed.</returns>
    internal static bool TryRead(ReadOnlySpan<byte> data, out int width, out int height, out int largestSide, out bool animated)
    {
        width = 0;
        height = 0;
        largestSide = 0;
        animated = false;
        var meta = default(ReadOnlySpan<byte>);
        var hasMeta = false;
        var first = true;
        var position = 0;
        while (position < data.Length)
        {
            if (!TryReadBox(data, position, allowToEnd: true, out var type, out var payload, out var next))
            {
                return false;
            }

            if (first != (type == FourCc.Ftyp))
            {
                return false;
            }

            switch (type)
            {
                case FourCc.Ftyp:
                    if (!TryReadBrands(payload, out animated))
                    {
                        return false;
                    }

                    break;
                case FourCc.Meta:
                    if (hasMeta)
                    {
                        return false;
                    }

                    hasMeta = true;
                    meta = payload;
                    break;
                case FourCc.Moov:
                    animated = true;
                    break;
                default:
                    break;
            }

            first = false;
            position = next;
        }

        return hasMeta && TryReadMeta(meta, out width, out height, out largestSide);
    }

    private static bool TryReadBrands(ReadOnlySpan<byte> payload, out bool sequence)
    {
        sequence = false;
        if (payload.Length < FtypFixedLength || (payload.Length - FtypFixedLength) % 4 != 0)
        {
            return false;
        }

        var isAvif = false;
        for (var position = 0; position < payload.Length; position += position == 0 ? 8 : 4)
        {
            var brand = BinaryPrimitives.ReadUInt32BigEndian(payload[position..]);
            isAvif |= brand is FourCc.Avif or FourCc.Avis;
            sequence |= brand == FourCc.Avis;
        }

        return isAvif;
    }

    private static bool TryReadMeta(ReadOnlySpan<byte> meta, out int width, out int height, out int largestSide)
    {
        width = 0;
        height = 0;
        largestSide = 0;
        if (meta.Length < FullBoxHeaderLength)
        {
            return false;
        }

        uint? primary = null;
        var properties = default(ReadOnlySpan<byte>);
        var hasProperties = false;
        var children = meta[FullBoxHeaderLength..];
        var position = 0;
        while (position < children.Length)
        {
            if (!TryReadBox(children, position, allowToEnd: false, out var type, out var payload, out position))
            {
                return false;
            }

            if (type == FourCc.Pitm)
            {
                if (primary is not null || !TryReadPrimaryItem(payload, out var item))
                {
                    return false;
                }

                primary = item;
            }
            else if (type == FourCc.Iprp)
            {
                if (hasProperties)
                {
                    return false;
                }

                hasProperties = true;
                properties = payload;
            }
        }

        return primary is { } primaryItem && hasProperties && TryReadProperties(properties, primaryItem, out width, out height, out largestSide);
    }

    private static bool TryReadPrimaryItem(ReadOnlySpan<byte> payload, out uint item)
    {
        item = 0;
        if (payload.Length < FullBoxHeaderLength)
        {
            return false;
        }

        var version = payload[0];
        var body = payload[FullBoxHeaderLength..];
        if (version == 0 && body.Length == 2)
        {
            item = BinaryPrimitives.ReadUInt16BigEndian(body);
            return true;
        }

        if (version == 1 && body.Length == 4)
        {
            item = BinaryPrimitives.ReadUInt32BigEndian(body);
            return true;
        }

        return false;
    }

    private static bool TryReadProperties(ReadOnlySpan<byte> iprp, uint primary, out int width, out int height, out int largestSide)
    {
        width = 0;
        height = 0;
        largestSide = 0;
        var container = default(ReadOnlySpan<byte>);
        var hasContainer = false;
        var associations = default(ReadOnlySpan<byte>);
        var hasAssociations = false;
        var position = 0;
        while (position < iprp.Length)
        {
            if (!TryReadBox(iprp, position, allowToEnd: false, out var type, out var payload, out position))
            {
                return false;
            }

            if (type == FourCc.Ipco)
            {
                if (hasContainer)
                {
                    return false;
                }

                hasContainer = true;
                container = payload;
            }
            else if (type == FourCc.Ipma)
            {
                if (hasAssociations)
                {
                    return false;
                }

                hasAssociations = true;
                associations = payload;
            }
        }

        if (!hasContainer || !hasAssociations || !TryScanProperties(container, out var count, out largestSide))
        {
            return false;
        }

        return TryReadPrimaryExtents(associations, container, primary, count, out width, out height);
    }

    /// <summary>Counts the properties and checks every <c>ispe</c> among them.</summary>
    private static bool TryScanProperties(ReadOnlySpan<byte> ipco, out int count, out int largestSide)
    {
        count = 0;
        largestSide = 0;
        var position = 0;
        while (position < ipco.Length)
        {
            if (!TryReadBox(ipco, position, allowToEnd: false, out var type, out var payload, out position))
            {
                return false;
            }

            count++;
            if (type == FourCc.Ispe)
            {
                if (!TryReadExtents(payload, out var width, out var height))
                {
                    return false;
                }

                largestSide = Math.Max(largestSide, Math.Max(width, height));
            }
        }

        return true;
    }

    /// <summary>
    /// Walks the associations and reads the <c>ispe</c> property associated with the primary item. The primary item may be listed once, so
    /// at most 255 properties are looked up.
    /// </summary>
    private static bool TryReadPrimaryExtents(
        ReadOnlySpan<byte> ipma,
        ReadOnlySpan<byte> ipco,
        uint primary,
        int propertyCount,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        if (ipma.Length < FullBoxHeaderLength + 4)
        {
            return false;
        }

        var version = ipma[0];
        var wideIndex = (ipma[3] & 1) != 0;
        var entries = BinaryPrimitives.ReadUInt32BigEndian(ipma[FullBoxHeaderLength..]);
        var body = ipma[(FullBoxHeaderLength + 4)..];
        var itemIdLength = version < 1 ? 2 : 4;
        var associationLength = wideIndex ? 2 : 1;
        var primarySeen = false;
        var found = false;
        var position = 0;
        for (var entry = 0u; entry < entries; entry++)
        {
            if (body.Length - position < itemIdLength + 1)
            {
                return false;
            }

            var item = itemIdLength == 2 ? BinaryPrimitives.ReadUInt16BigEndian(body[position..]) : BinaryPrimitives.ReadUInt32BigEndian(body[position..]);
            int associationCount = body[position + itemIdLength];
            position += itemIdLength + 1;
            if (body.Length - position < associationCount * associationLength)
            {
                return false;
            }

            var isPrimary = item == primary;
            if (isPrimary && primarySeen)
            {
                return false;
            }

            primarySeen |= isPrimary;
            for (var association = 0; association < associationCount; association++)
            {
                var index = wideIndex ? BinaryPrimitives.ReadUInt16BigEndian(body[position..]) & 0x7FFF : body[position] & 0x7F;
                position += associationLength;
                if (index > propertyCount)
                {
                    return false;
                }

                if (isPrimary && !found && index > 0 && TryReadExtentsAt(ipco, index, out width, out height))
                {
                    found = true;
                }
            }
        }

        return found && position == body.Length;
    }

    private static bool TryReadExtentsAt(ReadOnlySpan<byte> ipco, int index, out int width, out int height)
    {
        width = 0;
        height = 0;
        var position = 0;
        for (var current = 1; current <= index; current++)
        {
            if (!TryReadBox(ipco, position, allowToEnd: false, out var type, out var payload, out position))
            {
                return false;
            }

            if (current == index)
            {
                return type == FourCc.Ispe && TryReadExtents(payload, out width, out height);
            }
        }

        return false;
    }

    private static bool TryReadExtents(ReadOnlySpan<byte> payload, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (payload.Length != FullBoxHeaderLength + SpatialExtentsLength)
        {
            return false;
        }

        var rawWidth = BinaryPrimitives.ReadUInt32BigEndian(payload[FullBoxHeaderLength..]);
        var rawHeight = BinaryPrimitives.ReadUInt32BigEndian(payload[(FullBoxHeaderLength + 4)..]);
        if (rawWidth is 0 or > int.MaxValue || rawHeight is 0 or > int.MaxValue)
        {
            return false;
        }

        width = (int)rawWidth;
        height = (int)rawHeight;
        return true;
    }

    /// <summary>Reads one box header at <paramref name="position"/> and checks that the box fits in <paramref name="parent"/>.</summary>
    private static bool TryReadBox(ReadOnlySpan<byte> parent, int position, bool allowToEnd, out uint type, out ReadOnlySpan<byte> payload, out int next)
    {
        type = 0;
        payload = default;
        next = parent.Length;
        var remaining = parent.Length - position;
        if (remaining < BoxHeaderLength)
        {
            return false;
        }

        long size = BinaryPrimitives.ReadUInt32BigEndian(parent[position..]);
        type = BinaryPrimitives.ReadUInt32BigEndian(parent[(position + 4)..]);
        var headerLength = BoxHeaderLength;
        if (size == 1)
        {
            if (remaining < LargeBoxHeaderLength)
            {
                return false;
            }

            var large = BinaryPrimitives.ReadUInt64BigEndian(parent[(position + BoxHeaderLength)..]);
            if (large > (ulong)remaining)
            {
                return false;
            }

            size = (long)large;
            headerLength = LargeBoxHeaderLength;
        }
        else if (size == 0)
        {
            if (!allowToEnd)
            {
                return false;
            }

            size = remaining;
        }

        if (size < headerLength || size > remaining)
        {
            return false;
        }

        payload = parent.Slice(position + headerLength, (int)size - headerLength);
        next = position + (int)size;
        return true;
    }

    /// <summary>The box types and brands the reader uses, as big-endian integers of their four ASCII characters.</summary>
    private static class FourCc
    {
        internal const uint Ftyp = 0x66747970; // "ftyp"
        internal const uint Meta = 0x6D657461; // "meta"
        internal const uint Moov = 0x6D6F6F76; // "moov"
        internal const uint Pitm = 0x7069746D; // "pitm"
        internal const uint Iprp = 0x69707270; // "iprp"
        internal const uint Ipco = 0x6970636F; // "ipco"
        internal const uint Ipma = 0x69706D61; // "ipma"
        internal const uint Ispe = 0x69737065; // "ispe"
        internal const uint Avif = 0x61766966; // "avif"
        internal const uint Avis = 0x61766973; // "avis"
    }
}
