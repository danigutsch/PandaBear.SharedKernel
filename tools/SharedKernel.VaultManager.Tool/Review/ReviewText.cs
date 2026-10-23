using System.Text;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Turns names and values into the single-line text that the review shows.</summary>
internal static class ReviewText
{
    /// <summary>Shown in place of a masked value. Its length does not depend on the value.</summary>
    public const string Mask = "********";

    /// <summary>Shown in place of a line break inside a value.</summary>
    public const string LineBreak = " ↵ ";

    private const char Replacement = '�';

    /// <summary>
    /// Returns text on one line. Line breaks become <see cref="LineBreak"/>, tabs become spaces, and other control
    /// characters become U+FFFD, so a name or value can neither break the layout nor send control sequences to the
    /// terminal.
    /// </summary>
    /// <param name="text">Text, or <see langword="null"/>.</param>
    /// <returns>The text on one line; empty for <see langword="null"/>.</returns>
    public static string OneLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                builder.Append(LineBreak);
            }
            else if (character == '\t')
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(char.IsControl(character) ? Replacement : character);
            }
        }

        return builder.ToString();
    }

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
