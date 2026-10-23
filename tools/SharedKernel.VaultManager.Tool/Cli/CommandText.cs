using System.Globalization;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Formats the lines the commands print.</summary>
internal static class CommandText
{
    /// <summary>Writes a labeled count, such as <c>Items: 3</c>.</summary>
    /// <param name="writer">Writer.</param>
    /// <param name="label">Label, with its indentation.</param>
    /// <param name="count">Count.</param>
    public static void Count(TextWriter writer, string label, int count) =>
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{label}: {count}"));

    /// <summary>
    /// Shows every control character of a text as a space: the C0 controls, DEL, and the C1 controls, line breaks
    /// included. A name, path, or message from an export, a decisions file, or the command line then stays on one line
    /// and cannot send an escape sequence to the terminal.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns>The text without control characters.</returns>
    public static string OneLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Any(char.IsControl))
        {
            return text;
        }

        return string.Create(text.Length, text, static (shown, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                shown[index] = char.IsControl(source[index]) ? ' ' : source[index];
            }
        });
    }
}
