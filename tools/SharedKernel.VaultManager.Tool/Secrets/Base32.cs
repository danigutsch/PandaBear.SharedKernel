namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>RFC 4648 base32 helpers for TOTP secrets.</summary>
internal static class Base32
{
    /// <summary>Base32 alphabet.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Smallest accepted TOTP secret, in bits.</summary>
    public const int MinimumTotpBits = 80;

    /// <summary>Uppercases a secret and drops characters outside A-Z and 2-7, such as spaces, dashes, and padding.</summary>
    /// <param name="value">Raw secret.</param>
    /// <returns>Normalized secret.</returns>
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

    /// <summary>Whether text is a strict base32 TOTP secret of at least 80 bits: A-Z, 2-7, and optional padding.</summary>
    /// <param name="value">Candidate, already free of separators.</param>
    /// <returns><see langword="true"/> when the candidate qualifies.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsTotpSecret(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.TrimEnd('=');
        if (trimmed.Length * 5 < MinimumTotpBits)
        {
            return false;
        }

        foreach (char character in trimmed)
        {
            if (character is not ((>= 'A' and <= 'Z') or (>= '2' and <= '7')))
            {
                return false;
            }
        }

        return true;
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
