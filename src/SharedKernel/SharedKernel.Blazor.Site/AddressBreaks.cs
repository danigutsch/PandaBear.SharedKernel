using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// Renders text with a line-break opportunity (<c>wbr</c>) before every <c>@</c> and <c>.</c> inside a word that holds an <c>@</c>, such
/// as an e-mail address, so a narrow box wraps the address between its parts instead of inside a part. Every other word keeps its
/// characters together, so a number such as <c>8.5</c> never breaks.
/// </summary>
internal static class AddressBreaks
{
    /// <summary>Renders the text, HTML-encoded, with the break opportunities.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment Render(string text) => builder =>
    {
        var first = true;
        foreach (var segment in Segments(text))
        {
            if (!first)
            {
                builder.AddMarkupContent(0, "<wbr>");
            }

            builder.AddContent(1, segment);
            first = false;
        }
    };

    private static List<string> Segments(string text)
    {
        var segments = new List<string>();
        var start = 0;
        var wordStart = 0;
        while (wordStart < text.Length)
        {
            var wordEnd = wordStart;
            while (wordEnd < text.Length && !char.IsWhiteSpace(text[wordEnd]))
            {
                wordEnd++;
            }

            if (text.AsSpan(wordStart, wordEnd - wordStart).Contains('@'))
            {
                for (var index = wordStart + 1; index < wordEnd; index++)
                {
                    if (text[index] is '@' or '.')
                    {
                        segments.Add(text[start..index]);
                        start = index;
                    }
                }
            }

            wordStart = wordEnd + 1;
        }

        segments.Add(text[start..]);
        return segments;
    }
}
