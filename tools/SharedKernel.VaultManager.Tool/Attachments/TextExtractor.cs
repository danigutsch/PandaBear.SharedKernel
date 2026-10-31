using System.Text;

namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>Decodes attachment text within size limits, so later scanning works on bounded input.</summary>
internal static class TextExtractor
{
    /// <summary>Most bytes decoded from one file.</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>Most characters kept from one line.</summary>
    public const int MaxLineLength = 64 * 1024;

    /// <summary>Decodes UTF-8 content with the default limits.</summary>
    /// <param name="content">File content.</param>
    /// <returns>Extracted text and truncation details.</returns>
    public static ExtractedText Extract(ReadOnlySpan<byte> content) => Extract(content, MaxBytes, MaxLineLength);

    /// <summary>Decodes UTF-8 content, cutting the file at <paramref name="maxBytes"/> and each line at <paramref name="maxLineLength"/>.</summary>
    /// <param name="content">File content.</param>
    /// <param name="maxBytes">Most bytes decoded.</param>
    /// <param name="maxLineLength">Most characters kept per line; line breaks are kept.</param>
    /// <returns>Extracted text and truncation details.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> or <paramref name="maxLineLength"/> is zero or negative.</exception>
    public static ExtractedText Extract(ReadOnlySpan<byte> content, int maxBytes, int maxLineLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineLength);

        bool truncated = content.Length > maxBytes;
        ReadOnlySpan<byte> kept = truncated ? content[..maxBytes] : content;
        if (kept.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            kept = kept[3..];
        }

        // Invalid or cut multi-byte sequences become replacement characters instead of failing the whole file.
        string decoded = Encoding.UTF8.GetString(kept);
        var builder = new StringBuilder(decoded.Length);
        int truncatedLines = 0;
        int lineLength = 0;
        bool lineCut = false;
        foreach (char character in decoded)
        {
            if (character is '\n' or '\r')
            {
                builder.Append(character);
                lineLength = 0;
                lineCut = false;
                continue;
            }

            if (lineLength < maxLineLength)
            {
                builder.Append(character);
            }
            else if (!lineCut)
            {
                lineCut = true;
                truncatedLines++;
            }

            lineLength++;
        }

        return new ExtractedText(builder.ToString(), truncated, truncatedLines);
    }
}
