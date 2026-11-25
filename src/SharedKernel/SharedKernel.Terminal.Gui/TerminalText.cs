using SharedKernel.InputNormalization;

namespace SharedKernel.Terminal.Gui;

/// <summary>Turns labels and values into text that is safe for a single line of a terminal view.</summary>
public static class TerminalText
{
    /// <summary>Shown in place of a masked value. Its length does not depend on the value.</summary>
    public const string Mask = DisplayText.Mask;

    /// <summary>Shown in place of a line break inside a value.</summary>
    public const string LineBreak = DisplayText.LineBreak;

    /// <summary>
    /// Returns text on one line. Each line break that <see cref="string.ReplaceLineEndings(string)"/> recognizes, such as a
    /// line feed, a carriage return, or the line separator U+2028, becomes <see cref="LineBreak"/>. The rest is made
    /// printable by <see cref="PrintableText.OneLine"/>: tabs become spaces, and other control characters, format
    /// characters such as bidirectional marks, embeddings, overrides, and isolates and zero-width characters, other default
    /// ignorable code points except variation selectors, and unpaired surrogates become U+FFFD. A name or value can then
    /// neither break the layout, nor send control sequences to the terminal, nor reorder the text around it, nor hold a
    /// character that does not show.
    /// </summary>
    /// <param name="text">Text, or <see langword="null"/>.</param>
    /// <returns>The text on one line; empty for <see langword="null"/>.</returns>
    public static string OneLine(string? text) => DisplayText.OneLine(text);

    /// <summary>Shows a value on one line, or <see cref="Mask"/> when it is masked. An absent value shows as empty text.</summary>
    /// <param name="value">Value, or <see langword="null"/>.</param>
    /// <param name="masked">Whether to hide the value.</param>
    /// <returns>The text to show.</returns>
    public static string Value(string? value, bool masked) => DisplayText.Value(value, masked);
}
