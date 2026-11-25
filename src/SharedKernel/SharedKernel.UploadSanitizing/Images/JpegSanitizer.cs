namespace SharedKernel.UploadSanitizing.Images;

/// <summary>
/// Compacts a JPEG in place without its APP1 (Exif, XMP), APP13 (Photoshop, IPTC) and COM segments, without fill bytes and without bytes after
/// the end marker. JFIF (APP0), the ICC profile (APP2), the other segments and the entropy-coded scans stay unchanged. The size comes from the
/// single frame header.
/// </summary>
internal static class JpegSanitizer
{
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;

    /// <summary>Cleans a buffer that starts with the JPEG signature, moving the kept bytes towards its start.</summary>
    /// <param name="data">The file; overwritten with the result.</param>
    /// <param name="width">The width from the frame header, or zero.</param>
    /// <param name="height">The height from the frame header, or zero.</param>
    /// <returns>The length of the result, or <c>-1</c> when the structure is malformed.</returns>
    internal static int Clean(Span<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        var written = 2;
        var position = 2;
        var hasFrame = false;
        var inScan = false;
        while (true)
        {
            if (inScan)
            {
                var scanEnd = EndOfEntropyData(data, position);
                if (scanEnd < 0)
                {
                    return -1;
                }

                written = Move(data, position, scanEnd - position, written);
                position = scanEnd;
            }

            if (position >= data.Length || data[position] != 0xFF)
            {
                return -1;
            }

            // A marker may be preceded by fill bytes (0xFF); they carry nothing and are not kept.
            while (position < data.Length && data[position] == 0xFF)
            {
                position++;
            }

            if (position >= data.Length)
            {
                return -1;
            }

            var marker = data[position++];
            if (marker == EndOfImage)
            {
                if (!inScan)
                {
                    return -1;
                }

                data[written++] = 0xFF;
                data[written++] = EndOfImage;
                return written;
            }

            if (IsStandalone(marker) || data.Length - position < 2)
            {
                return -1;
            }

            var length = (data[position] << 8) | data[position + 1];
            if (length < 2 || length > data.Length - position)
            {
                return -1;
            }

            if (IsFrameHeader(marker))
            {
                // Only baseline-style files with one frame are accepted; a second frame header is a hierarchical or damaged file.
                if (hasFrame || length < 7)
                {
                    return -1;
                }

                hasFrame = true;
                height = (data[position + 3] << 8) | data[position + 4];
                width = (data[position + 5] << 8) | data[position + 6];
            }

            if (marker == StartOfScan)
            {
                if (!hasFrame)
                {
                    return -1;
                }

                inScan = true;
            }

            if (!IsMetadata(marker))
            {
                data[written++] = 0xFF;
                data[written++] = marker;
                written = Move(data, position, length, written);
            }

            position += length;
        }
    }

    // Removed: APP1 (Exif, XMP), APP13 (Photoshop, IPTC), COM.
    private static bool IsMetadata(byte marker) => marker is 0xE1 or 0xED or 0xFE;

    // SOF0..SOF15 except DHT (C4), JPG (C8) and DAC (CC).
    private static bool IsFrameHeader(byte marker) => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    // Markers without a length: TEM, RSTn, SOI, and the reserved 0x00. None is valid between segments.
    private static bool IsStandalone(byte marker) => marker is 0x00 or 0x01 or StartOfImage or >= 0xD0 and <= 0xD7;

    // Entropy-coded data ends at the first 0xFF that is neither a stuffed zero nor a restart marker.
    private static int EndOfEntropyData(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length)
        {
            var next = data[position..].IndexOf((byte)0xFF);
            if (next < 0)
            {
                return -1;
            }

            position += next;
            if (position + 1 >= data.Length)
            {
                return -1;
            }

            if (data[position + 1] is 0x00 or >= 0xD0 and <= 0xD7)
            {
                position += 2;
                continue;
            }

            return position;
        }

        return -1;
    }

    // The destination never passes the source, and Span.CopyTo handles the overlap.
    private static int Move(Span<byte> data, int from, int count, int to)
    {
        data.Slice(from, count).CopyTo(data[to..]);
        return to + count;
    }
}
