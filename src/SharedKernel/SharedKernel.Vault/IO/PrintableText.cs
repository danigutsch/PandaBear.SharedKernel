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
    public const char Replacement = '\uFFFD';

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
    /// to U+2069, the zero-width characters U+200B, U+2060, and U+FEFF, the soft hyphen U+00AD, and the tag characters
    /// U+E0001 and U+E0020 to U+E007F) becomes one <see cref="Replacement"/>, except a joiner or tag character in one of
    /// the sequences below.
    /// </description></item>
    /// <item><description>
    /// Every other code point with the Unicode property Default_Ignorable_Code_Point, other than a variation selector,
    /// becomes one <see cref="Replacement"/>: U+034F, the Hangul fillers U+115F, U+1160, U+3164, and U+FFA0, U+17B4,
    /// U+17B5, and the reserved U+2065, U+FFF0 to U+FFF8, U+E0000, U+E0002 to U+E001F, U+E0080 to U+E00FF, and U+E01F0
    /// to U+E0FFF.
    /// </description></item>
    /// <item><description>Each unpaired surrogate becomes one <see cref="Replacement"/>.</description></item>
    /// </list>
    /// The zero width joiner U+200D, the zero width non-joiner U+200C, and the tag characters stay only where they change
    /// how the text shows, and are replaced everywhere else, such as first, last, next to white space, or doubled:
    /// <list type="bullet">
    /// <item><description>
    /// U+200D between two emoji of an emoji ZWJ sequence (UTS #51 ED-16), such as a technologist: an Extended_Pictographic
    /// character, optionally followed by emoji modifiers and U+FE0F, or an emoji tag sequence, then U+200D, then an
    /// Extended_Pictographic character.
    /// </description></item>
    /// <item><description>
    /// U+200C where it breaks a cursive connection, as in Persian: a Dual_Joining or Left_Joining character, optionally
    /// followed by nonspacing or enclosing marks, then U+200C, then a Dual_Joining or Right_Joining character (UTS #39
    /// condition A1).
    /// </description></item>
    /// <item><description>
    /// U+200C or U+200D in a conjunct, as in Indic scripts: a letter followed by nonspacing marks of which one is a virama,
    /// then U+200C or U+200D, then a letter, which for U+200C may come after nonspacing marks (UTS #39 conditions A2 and B).
    /// </description></item>
    /// <item><description>
    /// The tag characters of a well-formed flag emoji tag sequence (UTS #51 ED-14a and Annex C): U+1F3F4, then three to
    /// seven tag digits or tag small letters U+E0030 to U+E0039 and U+E0061 to U+E007A, then U+E007F, such as the flag of
    /// England.
    /// </description></item>
    /// </list>
    /// A code point outside the Basic Multilingual Plane, two UTF-16 code units, also becomes a single
    /// <see cref="Replacement"/>, so the result can be shorter than <paramref name="text"/>. Every other code point,
    /// including a valid surrogate pair, such as an emoji, and a variation selector, stays as it is. The text can therefore
    /// neither start a line, nor carry a terminal escape sequence, nor reorder the text printed around it, nor hold a
    /// character that neither shows nor changes how the characters next to it show.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns>The printable text, or <paramref name="text"/> itself when it has no such character.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string OneLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.AsSpan().ContainsAnyExceptInRange(' ', '~'))
        {
            return text;
        }

        StringBuilder? printable = null;
        int index = 0;
        while (index < text.Length)
        {
            char? substitute = Substitute(text, index, out int length);
            if (substitute is char shown)
            {
                printable ??= new StringBuilder(text.Length).Append(text, 0, index);
                printable.Append(shown);
            }
            else
            {
                printable?.Append(text, index, length);
            }

            index += length;
        }

        return printable?.ToString() ?? text;
    }

    /// <summary>Returns <see cref="OneLine"/> text with every <c>|</c> escaped, for a Markdown table cell.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The cell text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string TableCell(string text) => OneLine(text).Replace("|", "\\|", StringComparison.Ordinal);

    // Returns the character printed in place of the code point or sequence at index, or null when it is printed as it is,
    // and the length of that code point or sequence in UTF-16 code units.
    private static char? Substitute(string text, int index, out int length)
    {
        if (Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out length) != OperationStatus.Done)
        {
            // An unpaired surrogate: a high surrogate without a low one after it, or a low one without a high one.
            length = Math.Max(length, 1);
            return Replacement;
        }

        switch (rune.Value)
        {
            case '\r' or '\n' or '\t' or 0x2028 or 0x2029:
                return ' ';
            case JoinerContexts.ZeroWidthJoiner or JoinerContexts.ZeroWidthNonJoiner:
                return JoinerContexts.IsKept(text, index) ? null : Replacement;
            case JoinerContexts.BlackFlag:
                length = Math.Max(length, JoinerContexts.EmojiTagSequenceLength(text, index));
                return null;
            default:
                return IsUnprintable(rune) ? Replacement : null;
        }
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
