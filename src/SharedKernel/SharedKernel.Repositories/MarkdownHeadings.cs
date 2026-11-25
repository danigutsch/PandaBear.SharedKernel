using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SharedKernel.Markdown;

/// <summary>Reads heading text and GitHub-compatible anchors outside fenced code.</summary>
internal static partial class MarkdownHeadings
{
    /// <summary>Reads heading levels, labels, and anchors in document order.</summary>
    public static IEnumerable<(int Level, string Title, string Anchor)> Read(IReadOnlyList<string> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, line) in NonFencedLines(lines))
        {
            var stripped = TrimStartSpace(line);
            if (!stripped.StartsWith('#', StringComparison.Ordinal))
            {
                continue;
            }

            var titleWithWhitespace = stripped.TrimStart('#');
            var level = stripped.Length - titleWithWhitespace.Length;
            var title = TrimSpace(titleWithWhitespace);
            if (title.Length == 0)
            {
                continue;
            }

            var slug = Slugify(title);
            var count = counts[slug] = counts.GetValueOrDefault(slug) + 1;
            var anchor = count == 1 ? slug : string.Create(CultureInfo.InvariantCulture, $"{slug}-{count - 1}");
            yield return (level, title, anchor);
        }
    }

    /// <summary>
    /// Returns the one-based number and content of each line outside fenced code. Fence lines are left out.
    /// </summary>
    public static IEnumerable<(int Number, string Line)> NonFencedLines(IReadOnlyList<string> lines)
    {
        var inFence = false;
        var fenceMarker = string.Empty;
        for (var index = 0; index < lines.Count; index++)
        {
            if (IsFence(lines[index], ref inFence, ref fenceMarker))
            {
                continue;
            }

            if (!inFence)
            {
                yield return (index + 1, lines[index]);
            }
        }
    }

    private static bool IsFence(string line, ref bool inFence, ref string fenceMarker)
    {
        var stripped = TrimStartSpace(line);
        if (!stripped.StartsWith("```", StringComparison.Ordinal) && !stripped.StartsWith("~~~", StringComparison.Ordinal))
        {
            return false;
        }

        var marker = stripped[..3];
        if (!inFence)
        {
            inFence = true;
            fenceMarker = marker;
        }
        else if (marker == fenceMarker)
        {
            inFence = false;
        }

        return true;
    }

    private static string Slugify(string heading)
    {
        var slug = new StringBuilder(heading.Length);
        foreach (var rune in Lower(HeadingText(HtmlTagPattern().Replace(heading, string.Empty))).EnumerateRunes())
        {
            if (rune.Value == ' ')
            {
                slug.Append('-');
            }
            else if (rune.Value == '-' || IsAnchorCategory(Rune.GetUnicodeCategory(rune)))
            {
                slug.Append(rune);
            }
        }

        return slug.ToString();
    }

    private static string HeadingText(string heading)
    {
        // Code spans render their content literally; elsewhere a pair of underscore runs around text is emphasis.
        var text = new StringBuilder(heading.Length);
        var start = 0;
        foreach (Match span in CodeSpanPattern().Matches(heading))
        {
            text.Append(EmphasisUnderscorePattern().Replace(heading[start..span.Index], "$2")).Append(span.Value);
            start = span.Index + span.Length;
        }

        return text.Append(EmphasisUnderscorePattern().Replace(heading[start..], "$2")).ToString();
    }

    private static bool IsAnchorCategory(UnicodeCategory category) => category
        is UnicodeCategory.UppercaseLetter
        or UnicodeCategory.LowercaseLetter
        or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter
        or UnicodeCategory.OtherLetter
        or UnicodeCategory.NonSpacingMark
        or UnicodeCategory.SpacingCombiningMark
        or UnicodeCategory.EnclosingMark
        or UnicodeCategory.DecimalDigitNumber
        or UnicodeCategory.LetterNumber
        or UnicodeCategory.ConnectorPunctuation;

    private static string TrimStartSpace(string value) => value[LeadingWhitespace(value)..];

    private static int LeadingWhitespace(string value)
    {
        var start = 0;
        while (start < value.Length && IsSpace(value[start]))
        {
            start++;
        }

        return start;
    }

    private static bool IsSpace(char character) => char.IsWhiteSpace(character) || character is >= '\x1C' and <= '\x1F';

    private static string TrimSpace(string value)
    {
        var start = LeadingWhitespace(value);
        var end = value.Length;
        while (end > start && IsSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    /// <summary>Lowercases heading text while preserving the dotted I expansion used by GitHub.</summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "GitHub matches lowercase anchors ordinally.")]
    public static string Lower(string value) =>
        value.Replace("\u0130", "i\u0307", StringComparison.Ordinal).ToLowerInvariant();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("(`+)(?!`).*?(?<!`)\\1(?!`)")]
    private static partial Regex CodeSpanPattern();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(_+)(?![\s_])(.+?)(?<![\s_])\1(?![\p{L}\p{N}])")]
    private static partial Regex EmphasisUnderscorePattern();
}
