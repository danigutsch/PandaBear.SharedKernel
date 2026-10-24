using System.Buffers;
using System.Globalization;
using System.Text;

namespace PandaBear.Tooling;

/// <summary>
/// Escapes text from files, processes, and the network before a tool writes it to a terminal or a CI log.
/// </summary>
internal static class ControlCharacters
{
    /// <summary>
    /// Replaces control, format, line separator, and paragraph separator characters, and lone surrogates, with
    /// <c>\uXXXX</c> or <c>\UXXXXXXXX</c> escapes. Other text, including non-ASCII letters and emoji, is kept.
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>The escaped text, or <paramref name="value"/> itself when nothing needs escaping.</returns>
    internal static string Escape(string value)
    {
        StringBuilder? escaped = null;
        var index = 0;
        while (index < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                escaped ??= new StringBuilder(value.Length + 6).Append(value, 0, index);
                escaped.Append("\\u").Append(((int)value[index]).ToString("X4", CultureInfo.InvariantCulture));
                index++;
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is not UnicodeCategory.Control
                    and not UnicodeCategory.Format
                    and not UnicodeCategory.LineSeparator
                    and not UnicodeCategory.ParagraphSeparator)
            {
                escaped?.Append(value, index, consumed);
                index += consumed;
                continue;
            }

            escaped ??= new StringBuilder(value.Length + 6).Append(value, 0, index);
            escaped
                .Append(rune.IsBmp ? "\\u" : "\\U")
                .Append(rune.Value.ToString(rune.IsBmp ? "X4" : "X8", CultureInfo.InvariantCulture));
            index += consumed;
        }

        return escaped?.ToString() ?? value;
    }
}
