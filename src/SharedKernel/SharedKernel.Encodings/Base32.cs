namespace PandaBear.Encodings;

/// <summary>RFC 4648 Base32, the encoding of TOTP secrets, which .NET does not provide.</summary>
public static class Base32
{
    /// <summary>Base32 alphabet.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Uppercases text and drops characters outside A-Z and 2-7, such as spaces, dashes, and padding.</summary>
    /// <param name="value">Raw text.</param>
    /// <returns>The normalized text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var characters = new char[value.Length];
        int written = 0;
        foreach (char character in value)
        {
            char upper = char.ToUpperInvariant(character);
            if (upper is (>= 'A' and <= 'Z') or (>= '2' and <= '7'))
            {
                characters[written++] = upper;
            }
        }

        return new string(characters, 0, written);
    }

    /// <summary>Encodes bytes without padding.</summary>
    /// <param name="data">Bytes.</param>
    /// <returns>Base32 text.</returns>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var characters = new char[((data.Length * 8) + 4) / 5];
        int buffer = 0;
        int bits = 0;
        int written = 0;
        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                characters[written++] = Alphabet[(buffer >> (bits - 5)) & 31];
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            characters[written++] = Alphabet[(buffer << (5 - bits)) & 31];
        }

        return new string(characters, 0, written);
    }
}
