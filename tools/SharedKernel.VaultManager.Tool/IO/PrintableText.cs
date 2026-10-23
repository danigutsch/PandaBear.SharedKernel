using System.Buffers;

namespace PandaBear.VaultManager.Tool.IO;

/// <summary>
/// Makes the text that the commands, the review, and the reports show safe to print in reports and on a terminal: names
/// read from a vault, such as item, folder, and file names, and paths, keys, warnings, and messages.
/// </summary>
internal static class PrintableText
{
    /// <summary>
    /// Character printed in place of a control character other than a line break or tab, and in place of a bidirectional
    /// embedding, override, or isolate: U+FFFD.
    /// </summary>
    public const char Replacement = '\uFFFD';

    // C0 controls, DEL, and C1 controls; the line and paragraph separators; and the bidirectional embeddings, overrides,
    // and isolates, which reorder the text around them.
    private static readonly SearchValues<char> Unprintable = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F"
        + "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F"
        + "\u007F\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F"
        + "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F"
        + "\u2028\u2029\u202A\u202B\u202C\u202D\u202E\u2066\u2067\u2068\u2069");

    /// <summary>
    /// Returns the text on one line: carriage returns, line feeds, tabs, and the line and paragraph separators U+2028 and
    /// U+2029 become spaces, and every other control character (C0, DEL, and C1) and every bidirectional embedding,
    /// override, and isolate (U+202A to U+202E and U+2066 to U+2069) becomes <see cref="Replacement"/>. The text can
    /// therefore neither start a line, nor carry a terminal escape sequence, nor reorder the text printed around it.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns>The printable text, or <paramref name="text"/> itself when it has no such character.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string OneLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.AsSpan().ContainsAny(Unprintable))
        {
            return text;
        }

        return string.Create(text.Length, text, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                span[index] = character switch
                {
                    '\r' or '\n' or '\t' or '\u2028' or '\u2029' => ' ',
                    _ when Unprintable.Contains(character) => Replacement,
                    _ => character,
                };
            }
        });
    }

    /// <summary>Returns <see cref="OneLine"/> text with every <c>|</c> escaped, for a Markdown table cell.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The cell text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string TableCell(string text) => OneLine(text).Replace("|", "\\|", StringComparison.Ordinal);
}
