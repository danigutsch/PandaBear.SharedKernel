using System.Buffers;
using System.Buffers.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Text helpers shared by detectors: entropy, character classes, line positions, and decoding.</summary>
internal static class SecretText
{
    /// <summary>Shannon entropy in bits per character.</summary>
    /// <param name="value">Text.</param>
    /// <returns>Entropy, or zero for empty text.</returns>
    public static double ShannonEntropy(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return 0;
        }

        var counts = new Dictionary<char, int>();
        foreach (char character in value)
        {
            counts[character] = counts.GetValueOrDefault(character) + 1;
        }

        double entropy = 0;
        foreach (int count in counts.Values)
        {
            double probability = (double)count / value.Length;
            entropy -= probability * Math.Log2(probability);
        }

        return entropy;
    }

    /// <summary>Number of character classes present: lowercase, uppercase, digits, and other.</summary>
    /// <param name="value">Text.</param>
    /// <returns>Class count from 0 to 4.</returns>
    public static int CharacterClasses(ReadOnlySpan<char> value)
    {
        bool lower = false;
        bool upper = false;
        bool digit = false;
        bool other = false;
        foreach (char character in value)
        {
            if (char.IsAsciiLetterLower(character))
            {
                lower = true;
            }
            else if (char.IsAsciiLetterUpper(character))
            {
                upper = true;
            }
            else if (char.IsAsciiDigit(character))
            {
                digit = true;
            }
            else
            {
                other = true;
            }
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
    }

    /// <summary>Whether the text contains a space, tab, carriage return, or line feed.</summary>
    /// <param name="value">Text.</param>
    /// <returns><see langword="true"/> when whitespace is present.</returns>
    public static bool HasWhitespace(ReadOnlySpan<char> value)
    {
        return value.IndexOfAny(" \t\r\n") >= 0;
    }

    /// <summary>Splits text into lines with their start offsets, without line terminators.</summary>
    /// <param name="text">Text.</param>
    /// <returns>Lines in order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static List<(int Start, string Text)> Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<(int, string)>();
        int start = 0;
        while (start <= text.Length)
        {
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline;
            int contentEnd = end > start && text[end - 1] == '\r' ? end - 1 : end;
            lines.Add((start, text[start..contentEnd]));
            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
        }

        return lines;
    }

    /// <summary>Decodes unpadded or padded base64url.</summary>
    /// <param name="value">Encoded text.</param>
    /// <param name="bytes">Decoded bytes.</param>
    /// <returns><see langword="true"/> when the text is valid base64url.</returns>
    public static bool TryDecodeBase64Url(ReadOnlySpan<char> value, out byte[]? bytes)
    {
        bytes = null;
        if (value.IsEmpty || value.Length > 65_536)
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(value.TrimEnd('='));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Decodes standard base64, ignoring ASCII whitespace.</summary>
    /// <param name="value">Encoded text.</param>
    /// <param name="bytes">Decoded bytes.</param>
    /// <returns><see langword="true"/> when the text is valid base64.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool TryDecodeBase64(string value, out byte[]? bytes)
    {
        ArgumentNullException.ThrowIfNull(value);
        bytes = null;
        string compact = string.Concat(value.Where(character => character is not (' ' or '\t' or '\r' or '\n')));
        if (compact.Length == 0 || compact.Length % 4 != 0)
        {
            return false;
        }

        byte[] buffer = new byte[compact.Length / 4 * 3];
        if (Convert.TryFromBase64String(compact, buffer, out int written))
        {
            bytes = buffer[..written];
            return true;
        }

        return false;
    }

    /// <summary>Whether the text uses only the base64 or base64url alphabet, with optional padding.</summary>
    /// <param name="value">Text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsBase64Alphabet(ReadOnlySpan<char> value)
    {
        return !value.TrimEnd('=').ContainsAnyExcept(Base64Characters);
    }

    /// <summary>Whether the text is hexadecimal.</summary>
    /// <param name="value">Text.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsHex(ReadOnlySpan<char> value)
    {
        return !value.ContainsAnyExcept(HexCharacters);
    }

    private static readonly SearchValues<char> Base64Characters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/-_");

    private static readonly SearchValues<char> HexCharacters = SearchValues.Create("0123456789abcdefABCDEF");
}
