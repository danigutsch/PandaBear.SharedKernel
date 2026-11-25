using System.Buffers;
using System.Globalization;
using System.Text;

namespace SharedKernel.InputNormalization;

/// <summary>
/// Escapes text from files, processes, and the network before a tool writes it to a terminal or a CI log.
/// </summary>
public static class ControlCharacters
{
    /// <summary>
    /// Replaces control, format, line separator, and paragraph separator characters, and lone surrogates, with
    /// <c>\uXXXX</c> or <c>\UXXXXXXXX</c> escapes. Other text, including non-ASCII letters and emoji, is kept.
    /// </summary>
    /// <param name="value">The text to escape.</param>
    /// <returns>The escaped text, or <paramref name="value"/> itself when nothing needs escaping.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
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

    /// <summary>
    /// Splits multi-line text, such as an exception message that carries process output, into its lines and escapes each
    /// line with <see cref="Escape"/>, so the text keeps its line structure but no line can carry a control character.
    /// Trailing line breaks are dropped, and a <c>\r</c> before a <c>\n</c> ends the line with it.
    /// </summary>
    /// <param name="value">The text to split and escape.</param>
    /// <returns>The escaped lines; one empty line for empty text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string[] EscapeLines(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return [.. value.TrimEnd('\r', '\n').Split('\n').Select(static line => Escape(line.TrimEnd('\r')))];
    }
}
