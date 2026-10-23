using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Turns names and values into the single-line text that the review shows.</summary>
internal static class ReviewText
{
    /// <summary>Shown in place of a masked value. Its length does not depend on the value.</summary>
    public const string Mask = "********";

    /// <summary>Shown in place of a line break inside a value.</summary>
    public const string LineBreak = " ↵ ";

    /// <summary>
    /// Returns text on one line. Each line break that <see cref="string.ReplaceLineEndings(string)"/> recognizes, such as a
    /// line feed, a carriage return, or the line separator U+2028, becomes <see cref="LineBreak"/>. The rest is made
    /// printable by <see cref="PrintableText.OneLine"/>: tabs become spaces, and other control characters and bidirectional
    /// embeddings, overrides, and isolates become U+FFFD. A name or value can then neither break the layout, nor send
    /// control sequences to the terminal, nor reorder the text around it.
    /// </summary>
    /// <param name="text">Text, or <see langword="null"/>.</param>
    /// <returns>The text on one line; empty for <see langword="null"/>.</returns>
    public static string OneLine(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : PrintableText.OneLine(text.ReplaceLineEndings(LineBreak));

    /// <summary>Shows a value on one line, or <see cref="Mask"/> when it is masked. An absent value shows as empty text.</summary>
    /// <param name="value">Value, or <see langword="null"/>.</param>
    /// <param name="masked">Whether to hide the value.</param>
    /// <returns>The text to show.</returns>
    public static string Value(string? value, bool masked)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return masked ? Mask : OneLine(value);
    }
}
