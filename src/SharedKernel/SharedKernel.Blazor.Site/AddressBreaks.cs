using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// Renders text with a line-break opportunity (<c>wbr</c>) before every <c>@</c> and <c>.</c> inside a word that holds an <c>@</c>, such
/// as an e-mail address, so a narrow box wraps the address between its parts instead of inside a part. A dot that ends the word, or is
/// followed only by punctuation such as a closing bracket, gets none, so a full stop never starts a line. Every other word keeps its
/// characters together, so a number such as <c>8.5</c> never breaks. Chromium puts a space at every <c>wbr</c> when it computes a name
/// from content, so a link whose text gets breaks needs the text as its <c>aria-label</c>; see <see cref="HasBreaks" />.
/// </summary>
internal static class AddressBreaks
{
    /// <summary>Gets whether the text gets at least one break opportunity.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true" /> when <see cref="Render" /> writes a <c>wbr</c>.</returns>
    public static bool HasBreaks(string text) => Segments(text).Count > 1;

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
                    if (text[index] == '@' || (text[index] == '.' && !EndsInPunctuation(text, index + 1, wordEnd)))
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

    private static bool EndsInPunctuation(string text, int from, int wordEnd)
    {
        for (var index = from; index < wordEnd; index++)
        {
            if (!char.IsPunctuation(text[index]))
            {
                return false;
            }
        }

        return true;
    }
}
