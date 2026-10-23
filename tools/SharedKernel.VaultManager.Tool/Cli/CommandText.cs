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

    /// <summary>Joins the lines of a text, so a name or warning cannot start a line of its own.</summary>
    /// <param name="text">Text.</param>
    /// <returns>The text on one line.</returns>
    public static string OneLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ');
}
