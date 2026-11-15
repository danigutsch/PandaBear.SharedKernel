using System.Buffers;
using System.Globalization;
using System.Text;

namespace PandaBear.Vault.IO;

/// <summary>
/// Makes the text that the commands, the review, and the reports show safe to print in reports and on a terminal: names
/// read from a vault, such as item, folder, and file names, and paths, keys, warnings, and messages.
/// </summary>
public static class PrintableText
{
    /// <summary>
    /// Character printed in place of a control character other than a line break or tab, a format character such as a
    /// bidirectional mark, embedding, override, or isolate or a zero-width character, another default ignorable code
    /// point, and an unpaired surrogate: U+FFFD.
    /// </summary>
    public const char Replacement = '�';

    /// <summary>
    /// Returns the text on one line, with every character that is invisible or changes how the text around it shows
    /// neutralized, code point by code point:
    /// <list type="bullet">
    /// <item><description>
    /// Carriage returns, line feeds, tabs, and the line and paragraph separators U+2028 and U+2029 become spaces.
    /// </description></item>
    /// <item><description>
    /// Every other code point in the general category Cc (control: C0, DEL, and C1) or Cf (format, such as the
    /// bidirectional marks U+200E, U+200F, and U+061C, the embeddings, overrides, and isolates U+202A to U+202E and U+2066
    /// to U+2069, the zero-width characters U+200B to U+200D, U+2060, and U+FEFF, the soft hyphen U+00AD, and the tag
    /// characters U+E0001 and U+E0020 to U+E007F) becomes one <see cref="Replacement"/>.
    /// </description></item>
    /// <item><description>
    /// Every other code point with the Unicode property Default_Ignorable_Code_Point, other than a variation selector,
    /// becomes one <see cref="Replacement"/>: U+034F, the Hangul fillers U+115F, U+1160, U+3164, and U+FFA0, U+17B4,
    /// U+17B5, and the reserved U+2065, U+FFF0 to U+FFF8, U+E0000, U+E0002 to U+E001F, U+E0080 to U+E00FF, and U+E01F0
    /// to U+E0FFF.
    /// </description></item>
    /// <item><description>Each unpaired surrogate becomes one <see cref="Replacement"/>.</description></item>
    /// </list>
    /// A code point outside the Basic Multilingual Plane, two UTF-16 code units, also becomes a single
    /// <see cref="Replacement"/>, so the result can be shorter than <paramref name="text"/>. Every other code point,
    /// including a valid surrogate pair such as an emoji, stays as it is. The text can therefore neither start a line, nor
    /// carry a terminal escape sequence, nor reorder the text printed around it, nor hold a character that does not show.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns>The printable text, or <paramref name="text"/> itself when it has no such character.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string OneLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int first = IndexOfUnprintable(text);
        if (first < 0)
        {
            return text;
        }

        var printable = new StringBuilder(text.Length);
        printable.Append(text, 0, first);
        int index = first;
        while (index < text.Length)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int length) != OperationStatus.Done)
            {
                // An unpaired surrogate: a high surrogate without a low one after it, or a low one without a high one.
                printable.Append(Replacement);
                index += Math.Max(length, 1);
                continue;
            }

            if (rune.Value is '\r' or '\n' or '\t' or 0x2028 or 0x2029)
            {
                printable.Append(' ');
            }
            else if (IsUnprintable(rune))
            {
                printable.Append(Replacement);
            }
            else
            {
                printable.Append(text, index, length);
            }

            index += length;
        }

        return printable.ToString();
    }

    /// <summary>Returns <see cref="OneLine"/> text with every <c>|</c> escaped, for a Markdown table cell.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The cell text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string TableCell(string text) => OneLine(text).Replace("|", "\\|", StringComparison.Ordinal);

    private static int IndexOfUnprintable(string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int length) != OperationStatus.Done
                || IsUnprintable(rune))
            {
                return index;
            }

            index += length;
        }

        return -1;
    }

    private static bool IsUnprintable(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator
        || IsOtherDefaultIgnorable(rune.Value);

    // The code points with Default_Ignorable_Code_Point=Yes (DerivedCoreProperties.txt, Unicode 18.0) outside Cf, other
    // than the variation selectors U+180B to U+180D, U+180F, U+FE00 to U+FE0F, and U+E0100 to U+E01EF, which select the
    // glyph of the character before them and are part of emoji and ideographic variation sequences. The reserved ones
    // are kept for future format characters, so they are neutralized before a terminal with newer Unicode data hides them.
    private static bool IsOtherDefaultIgnorable(int codePoint) =>
        codePoint is 0x034F or 0x115F or 0x1160 or 0x17B4 or 0x17B5 or 0x2065 or 0x3164 or 0xFFA0
            or (>= 0xFFF0 and <= 0xFFF8)
            or (>= 0xE0000 and <= 0xE00FF)
            or (>= 0xE01F0 and <= 0xE0FFF);
}
