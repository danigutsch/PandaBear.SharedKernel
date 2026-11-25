using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace SharedKernel.InputNormalization;

/// <summary>
/// Provides string sanitization methods for domain inputs.
/// </summary>
public static partial class StringSanitizer
{
    /// <summary>
    /// Sanitizes like <see cref="Sanitize"/>, but reports malformed UTF-16 (a lone or reversed surrogate) and text that Unicode normalization
    /// refuses as <see langword="false"/> instead of throwing <see cref="ArgumentException"/>. Use it for untrusted input, such as form fields
    /// and route values, and turn <see langword="false"/> into a validation error.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <param name="sanitized">The sanitized string; <see langword="null"/> when <paramref name="value"/> is <see langword="null"/> or malformed.</param>
    /// <returns><see langword="false"/> when <paramref name="value"/> is malformed; <see langword="true"/> otherwise, including for <see langword="null"/>.</returns>
    public static bool TrySanitize(string? value, out string? sanitized)
    {
        sanitized = null;
        if (value is null)
        {
            return true;
        }

        if (!TryNormalize(value, out var normalized))
        {
            return false;
        }

        sanitized = NormalizeWhitespace(RemoveControlCharacters(normalized).Trim());
        return true;
    }

    /// <summary>Sanitizes like <see cref="SanitizeNotes"/>, but reports malformed text as <see langword="false"/> instead of throwing.</summary>
    /// <param name="value">The string to sanitize.</param>
    /// <param name="sanitized">The sanitized string; <see langword="null"/> when <paramref name="value"/> is <see langword="null"/> or malformed.</param>
    /// <returns><see langword="false"/> when <paramref name="value"/> is malformed; <see langword="true"/> otherwise, including for <see langword="null"/>.</returns>
    public static bool TrySanitizeNotes(string? value, out string? sanitized)
    {
        sanitized = null;
        if (value is null)
        {
            return true;
        }

        if (!TryNormalize(value, out var normalized))
        {
            return false;
        }

        sanitized = normalized.Trim();
        return true;
    }

    /// <summary>
    /// Splits plain text into paragraphs on blank lines, for text a person typed into one box, such as a description. Each paragraph is
    /// sanitized like <see cref="Sanitize"/>, so the line breaks inside it become single spaces, and empty paragraphs are dropped. Joining
    /// the paragraphs with a blank line gives the same paragraphs again. Malformed text is reported as <see langword="false"/>, as
    /// <see cref="TrySanitize"/> does.
    /// </summary>
    /// <param name="value">
    /// The text. Each Unicode line terminator ends a line: a line feed, a carriage return, both together, a next line (U+0085), a form
    /// feed (U+000C), a line separator (U+2028) and a paragraph separator (U+2029). A blank line between two lines ends a paragraph.
    /// </param>
    /// <param name="paragraphs">The paragraphs in order; empty when the text is missing, blank or malformed.</param>
    /// <returns><see langword="false"/> when <paramref name="value"/> is malformed; <see langword="true"/> otherwise.</returns>
    public static bool TrySanitizeParagraphs(string? value, out IReadOnlyList<string> paragraphs)
    {
        paragraphs = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryNormalize(value, out var normalized))
        {
            return false;
        }

        var found = new List<string>();
        var current = new StringBuilder();
        foreach (var line in normalized.ReplaceLineEndings("\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                AddParagraph(current, found);
            }
            else
            {
                current.Append(line).Append(' ');
            }
        }

        AddParagraph(current, found);
        paragraphs = found.AsReadOnly();
        return true;
    }

    /// <summary>Normalizes like <see cref="NormalizeKey"/>, but reports malformed text as <see langword="false"/> instead of throwing.</summary>
    /// <param name="value">The string to normalize.</param>
    /// <param name="key">The normalized key, or <see langword="null"/> when <paramref name="value"/> is malformed.</param>
    /// <returns><see langword="false"/> when <paramref name="value"/> is malformed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool TryNormalizeKey(string value, [NotNullWhen(true)] out string? key)
    {
        ArgumentNullException.ThrowIfNull(value);

        key = TrySanitize(value, out var sanitized) ? sanitized!.ToUpperInvariant() : null;
        return key is not null;
    }

    /// <summary>
    /// Sanitizes a string for general use (names, identifiers).
    /// Trims, normalizes whitespace, removes control characters, and normalizes Unicode.
    /// Returns null only if the input is null.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <returns>The sanitized string, or null if input is null.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not well-formed UTF-16; <see cref="TrySanitize"/> reports it instead.</exception>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Sanitize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        value = value.Normalize(NormalizationForm.FormC);
        value = RemoveControlCharacters(value);
        value = value.Trim();
        value = NormalizeWhitespace(value);

        return value;
    }

    /// <summary>
    /// Sanitizes a string for notes/comments where newlines and formatting might be intentional.
    /// Only trims and normalizes Unicode.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <returns>The sanitized string, or null if input is null.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not well-formed UTF-16; <see cref="TrySanitizeNotes"/> reports it instead.</exception>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? SanitizeNotes(string? value)
    {
        if (value is null)
        {
            return null;
        }

        value = value.Normalize(NormalizationForm.FormC);
        value = value.Trim();

        return value;
    }

    /// <summary>
    /// Sanitizes a collection of strings by removing null/empty entries, trimming, and removing duplicates.
    /// </summary>
    /// <param name="values">The collection to sanitize.</param>
    /// <returns>A sanitized array of unique, non-empty strings.</returns>
    public static string[] SanitizeCollection(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return
        [
            ..values
                .Select(Sanitize)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Produces a culture-invariant comparison key that ignores case and diacritics.
    /// </summary>
    /// <param name="value">String value to normalize.</param>
    /// <returns>Normalized comparison key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string NormalizeKeyRemovingDiacritics(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = NormalizeKey(value);
        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        var sortKey = compareInfo.GetSortKey(
            normalized,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

        return Convert.ToHexString(sortKey.KeyData);
    }

    /// <summary>
    /// Normalizes a string by applying general string sanitization and converting to uppercase.
    /// </summary>
    /// <param name="value">String value to normalize.</param>
    /// <returns>Normalized string value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static string NormalizeKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Sanitize(value).ToUpperInvariant();
    }

    /// <summary>Normalizes to form C, or reports text that is not well-formed UTF-16 or that normalization refuses.</summary>
    private static bool TryNormalize(string value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (!Utf16.IsValid(value))
        {
            return false;
        }

        try
        {
            // Well-formed text can still be refused, depending on the platform's Unicode data; that refusal is reported, not thrown.
            normalized = value.Normalize(NormalizationForm.FormC);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void AddParagraph(StringBuilder current, List<string> paragraphs)
    {
        var paragraph = NormalizeWhitespace(RemoveControlCharacters(current.ToString()).Trim());
        if (paragraph.Length > 0)
        {
            paragraphs.Add(paragraph);
        }

        current.Clear();
    }

    private static string RemoveControlCharacters(string value) => ControlCharacterRegex().Replace(value, string.Empty);

    private static string NormalizeWhitespace(string value) => MultipleWhitespaceRegex().Replace(value, " ");

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled)]
    private static partial Regex ControlCharacterRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceRegex();
}
