using System.Text.RegularExpressions;

namespace PandaBear.Blazor;

/// <summary>Bounds and screens text before it reaches a user-facing surface.</summary>
internal static partial class BoundedText
{
    private const string Ellipsis = "…";

    /// <summary>Truncates to at most <paramref name="maxLength" /> characters, ending in an ellipsis when cut.</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var keep = Math.Max(maxLength - Ellipsis.Length, 0);
        if (keep > 0 && char.IsHighSurrogate(value[keep - 1]))
        {
            keep--;
        }

        return string.Concat(value.AsSpan(0, keep), Ellipsis);
    }

    /// <summary>Replaces control characters with spaces and truncates, for text that must be shown whatever it contains.</summary>
    public static string ToDisplay(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                span[index] = char.IsControl(source[index]) ? ' ' : source[index];
            }
        });
        return Truncate(cleaned.Trim(), maxLength);
    }

    /// <summary>Returns a bounded single-line message, or <see langword="null" /> when the text is blank, multi-line, or stack-trace-like.</summary>
    public static string? ToSingleLineMessage(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || StackTraceLike().IsMatch(value))
        {
            return null;
        }

        return Truncate(value.Trim(), maxLength);
    }

    [GeneratedRegex(@"(^|\s)at\s+[\w.<>`$+]+\(|:line\s+\d+|\b[\w.]*Exception\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex StackTraceLike();
}
