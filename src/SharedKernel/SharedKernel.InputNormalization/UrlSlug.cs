using System.Globalization;
using System.Text;

namespace PandaBear.InputNormalization;

/// <summary>
/// Creates and validates canonical URL slugs: lowercase ASCII letters and digits joined by single hyphens.
/// </summary>
public static class UrlSlug
{
    /// <summary>
    /// The default maximum slug length.
    /// </summary>
    public const int DefaultMaxLength = 128;

    /// <summary>
    /// Attempts to normalize free text to a canonical slug.
    /// Diacritics are stripped (for example <c>Patagônia</c> becomes <c>patagonia</c>), whitespace, hyphens, and underscores
    /// collapse to a single hyphen, and input with control characters, path or query delimiters (<c>/ \ ? #</c>), or any other
    /// non-ASCII character is rejected rather than transliterated.
    /// </summary>
    /// <param name="value">The free text to normalize.</param>
    /// <param name="slug">The canonical slug when the method succeeds; otherwise <see cref="string.Empty" />.</param>
    /// <param name="maxLength">The maximum accepted slug length.</param>
    /// <returns><see langword="true" /> when the input yields a non-empty slug within <paramref name="maxLength" />; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength" /> is not positive.</exception>
    public static bool TryNormalize(string? value, out string slug, int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        slug = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsControl)
            || value.IndexOfAny(['/', '\\', '?', '#']) >= 0)
        {
            return false;
        }

        var builder = new StringBuilder(value.Length);
        var requiresSeparator = false;
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (requiresSeparator)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                requiresSeparator = false;
                continue;
            }

            if (char.IsWhiteSpace(character) || character is '-' or '_')
            {
                requiresSeparator = builder.Length > 0;
                continue;
            }

            return false;
        }

        if (builder.Length == 0 || builder.Length > maxLength)
        {
            return false;
        }

        slug = builder.ToString();
        return true;
    }

    /// <summary>
    /// Determines whether a value is already a canonical slug.
    /// </summary>
    /// <param name="value">The candidate slug.</param>
    /// <param name="maxLength">The maximum accepted slug length.</param>
    /// <returns><see langword="true" /> when normalizing the value returns it unchanged; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength" /> is not positive.</exception>
    public static bool IsCanonical(string? value, int maxLength = DefaultMaxLength)
    {
        return TryNormalize(value, out var slug, maxLength)
            && string.Equals(value, slug, StringComparison.Ordinal);
    }
}
