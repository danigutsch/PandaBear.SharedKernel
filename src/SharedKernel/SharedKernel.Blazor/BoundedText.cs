using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PandaBear.Blazor;

/// <summary>Bounds and screens text before it reaches a user-facing surface.</summary>
internal static partial class BoundedText
{
    private const string Ellipsis = "\u2026";
    private const int ScreeningLengthFactor = 4;

    /// <summary>Truncates to at most <paramref name="maxLength" /> characters, ending in an ellipsis when cut.</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        var keep = maxLength - Ellipsis.Length;
        if (keep > 0 && char.IsHighSurrogate(value[keep - 1]))
        {
            keep--;
        }

        return string.Concat(value.AsSpan(0, Math.Max(keep, 0)), Ellipsis);
    }

    /// <summary>
    /// Replaces control characters with spaces, removes Unicode format characters (bidirectional overrides and
    /// isolates, zero-width characters), and truncates, for text that must be shown whatever it contains.
    /// </summary>
    public static string ToDisplay(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength * ScreeningLengthFactor));
        foreach (var character in value)
        {
            if (IsFormat(character))
            {
                continue;
            }

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return Truncate(builder.ToString().Trim(), maxLength);
    }

    /// <summary>
    /// Returns a bounded single-line message, or <see langword="null" /> when the text is blank, longer than four
    /// times the bound (rejected before any pattern runs), multi-line, or stack-trace-like.
    /// </summary>
    public static string? ToSingleLineMessage(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength * ScreeningLengthFactor)
        {
            return null;
        }

        var cleaned = string.Concat(value.Where(character => !IsFormat(character)));
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Any(char.IsControl))
        {
            return null;
        }

        try
        {
            if (StackTraceLike().IsMatch(cleaned))
            {
                return null;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        return Truncate(cleaned.Trim(), maxLength);
    }

    private static bool IsFormat(char character) => char.GetUnicodeCategory(character) == UnicodeCategory.Format;

    [GeneratedRegex(@"(^|\s)at\s+[\w<>`$+]+(\.[\w<>`$+]+)+\(|\.\w+:line\s+\d+|\b\w+Exception\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex StackTraceLike();
}
