namespace PandaBear.Encodings;

/// <summary>
/// Bech32 as BIP-173 specifies it (not Bech32m), which is what age uses for recipients and identities. age lifts the
/// 90-character limit, so the limit is a parameter. The checksum catches a mistyped or truncated string, so a value that
/// decodes is the value that was meant.
/// </summary>
public static class Bech32
{
    /// <summary>The BIP-173 length limit that age does not apply.</summary>
    public const int Bip173MaxLength = 90;

    private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
    private const int ChecksumLength = 6;
    private static readonly int[] Generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];

    /// <summary>Decodes <paramref name="text"/> and verifies its checksum.</summary>
    /// <param name="text">The candidate, all lowercase or all uppercase.</param>
    /// <param name="maxLength">The longest accepted text, or <see langword="null"/> for no limit.</param>
    /// <param name="humanReadablePart">The lowercase human-readable part.</param>
    /// <param name="data">The 5-bit data values without the checksum.</param>
    /// <returns><see langword="true"/> when the text is valid Bech32.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static bool TryDecode(string text, int? maxLength, out string humanReadablePart, out byte[] data)
    {
        ArgumentNullException.ThrowIfNull(text);
        humanReadablePart = string.Empty;
        data = [];
        if (text.Length == 0 || (maxLength is { } limit && text.Length > limit))
        {
            return false;
        }

        var hasLower = false;
        var hasUpper = false;
        var lower = new char[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character is < (char)33 or > (char)126)
            {
                return false;
            }

            hasLower |= char.IsAsciiLetterLower(character);
            hasUpper |= char.IsAsciiLetterUpper(character);
            lower[index] = char.IsAsciiLetterUpper(character) ? (char)(character | 0x20) : character;
        }

        if (hasLower && hasUpper)
        {
            return false;
        }

        var separator = Array.LastIndexOf(lower, '1');
        if (separator < 1 || lower.Length - separator - 1 < ChecksumLength)
        {
            return false;
        }

        var values = new byte[lower.Length - separator - 1];
        for (var index = 0; index < values.Length; index++)
        {
            var value = Charset.IndexOf(lower[separator + 1 + index], StringComparison.Ordinal);
            if (value < 0)
            {
                return false;
            }

            values[index] = (byte)value;
        }

        var hrp = new string(lower, 0, separator);
        if (Polymod(hrp, values) != 1)
        {
            return false;
        }

        humanReadablePart = hrp;
        data = values[..^ChecksumLength];
        return true;
    }

    /// <summary>Regroups the 5-bit values that <see cref="TryDecode"/> returns into bytes. Padding must be fewer than five bits, all zero.</summary>
    /// <param name="fiveBit">The 5-bit values.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns><see langword="true"/> when the padding is valid.</returns>
    public static bool TryConvertToBytes(ReadOnlySpan<byte> fiveBit, out byte[] bytes)
    {
        List<byte> result = [];
        var accumulator = 0;
        var bits = 0;
        foreach (var value in fiveBit)
        {
            accumulator = (accumulator << 5) | value;
            bits += 5;
            while (bits >= 8)
            {
                bits -= 8;
                result.Add((byte)((accumulator >> bits) & 0xff));
            }

            accumulator &= (1 << bits) - 1;
        }

        bytes = [.. result];
        return bits < 5 && accumulator == 0;
    }

    private static int Polymod(string humanReadablePart, byte[] values)
    {
        var checksum = 1;

        void Step(int value)
        {
            var top = checksum >> 25;
            checksum = ((checksum & 0x1ffffff) << 5) ^ value;
            for (var index = 0; index < 5; index++)
            {
                if (((top >> index) & 1) != 0)
                {
                    checksum ^= Generator[index];
                }
            }
        }

        foreach (var character in humanReadablePart)
        {
            Step(character >> 5);
        }

        Step(0);
        foreach (var character in humanReadablePart)
        {
            Step(character & 31);
        }

        foreach (var value in values)
        {
            Step(value);
        }

        return checksum;
    }
}
