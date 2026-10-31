namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Removes placed values from source text, dropping lines left empty and applying edits in descending offset order.</summary>
internal static class SourceTextEditor
{
    /// <summary>Removes locations from text.</summary>
    /// <param name="text">Source text.</param>
    /// <param name="locations">Locations inside the text.</param>
    /// <returns>Remaining text, or <see langword="null"/> when nothing but whitespace remains.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="locations"/> is <see langword="null"/>.</exception>
    public static string? Remove(string text, IEnumerable<SecretLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(locations);
        var ranges = new List<(int Start, int End)>();
        foreach (SecretLocation location in locations)
        {
            ranges.Add(location.FirstLine is { } first ? LineRange(text, first, location.LastLine ?? first) : SpanRange(text, location.Start, location.End));
        }

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges)
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
            }
            else
            {
                merged.Add(range);
            }
        }

        string result = text;
        for (int index = merged.Count - 1; index >= 0; index--)
        {
            result = result.Remove(merged[index].Start, merged[index].End - merged[index].Start);
        }

        result = result.TrimEnd();
        return result.Trim().Length == 0 ? null : result;
    }

    private static (int Start, int End) LineRange(string text, int firstLine, int lastLine)
    {
        int start = LineStart(text, firstLine);
        int next = LineStart(text, lastLine + 1);
        if (next < 0)
        {
            // Last line of the text: take the newline before the block instead of after it.
            return (start > 0 ? PrecedingNewline(text, start) : 0, text.Length);
        }

        return (start, next);
    }

    private static (int Start, int End) SpanRange(string text, int start, int end)
    {
        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        int lineEnd = text.IndexOf('\n', end);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        bool emptyBefore = text.AsSpan(lineStart, start - lineStart).Trim().IsEmpty;
        bool emptyAfter = text.AsSpan(end, lineEnd - end).Trim().IsEmpty;
        if (emptyBefore && emptyAfter)
        {
            if (lineEnd < text.Length)
            {
                return (lineStart, lineEnd + 1);
            }

            return (lineStart > 0 ? PrecedingNewline(text, lineStart) : 0, text.Length);
        }

        if (emptyAfter)
        {
            int trimmedStart = start;
            while (trimmedStart > lineStart && text[trimmedStart - 1] is ' ' or '\t')
            {
                trimmedStart--;
            }

            return (trimmedStart, lineEnd);
        }

        return (start, end);
    }

    private static int LineStart(string text, int line)
    {
        int offset = 0;
        for (int current = 1; current < line; current++)
        {
            int newline = text.IndexOf('\n', offset);
            if (newline < 0)
            {
                return -1;
            }

            offset = newline + 1;
        }

        return offset <= text.Length ? offset : -1;
    }

    private static int PrecedingNewline(string text, int lineStart)
    {
        int start = lineStart - 1;
        return start > 0 && text[start - 1] == '\r' ? start - 1 : start;
    }
}
