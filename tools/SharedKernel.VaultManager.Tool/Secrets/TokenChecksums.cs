namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Checksums of prefixed tokens. GitHub and npm tokens end in six base62 characters that encode the CRC-32 of the
/// 30 random characters before them, as GitHub described in its 2021 token format announcement.
/// </summary>
internal static class TokenChecksums
{
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int RandomLength = 30;
    private const int ChecksumLength = 6;
    private static readonly uint[] Table = BuildTable();

    /// <summary>Whether a <c>gh?_</c> token carries a valid checksum.</summary>
    /// <param name="token">Token including its prefix.</param>
    /// <returns><see langword="true"/> when the checksum matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public static bool IsValidGitHubToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Length == 40 && token[3] == '_' && ChecksumMatches(token.AsSpan(4));
    }

    /// <summary>Whether an <c>npm_</c> token carries a valid checksum.</summary>
    /// <param name="token">Token including its prefix.</param>
    /// <returns><see langword="true"/> when the checksum matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public static bool IsValidNpmToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Length == 40 && token.StartsWith("npm_", StringComparison.Ordinal) && ChecksumMatches(token.AsSpan(4));
    }

    /// <summary>CRC-32 (IEEE 802.3) of bytes.</summary>
    /// <param name="data">Input bytes.</param>
    /// <returns>The checksum.</returns>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static bool ChecksumMatches(ReadOnlySpan<char> body)
    {
        if (body.Length != RandomLength + ChecksumLength)
        {
            return false;
        }

        Span<byte> random = stackalloc byte[RandomLength];
        for (int index = 0; index < RandomLength; index++)
        {
            char character = body[index];
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }

            random[index] = (byte)character;
        }

        ulong decoded = 0;
        foreach (char character in body[RandomLength..])
        {
            int digit = Base62Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (digit < 0)
            {
                return false;
            }

            decoded = (decoded * 62) + (ulong)digit;
        }

        return decoded == Crc32(random);
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320 : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
