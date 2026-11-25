namespace SharedKernel.InputNormalization;

/// <summary>Formats untrusted text and masks values for single-line display.</summary>
public static class DisplayText
{
    /// <summary>The display value for a masked secret, independent of its original length.</summary>
    public const string Mask = "********";

    /// <summary>The display value for a line break.</summary>
    public const string LineBreak = " ↵ ";

    /// <summary>Replaces line endings with <see cref="LineBreak"/> and makes the remaining text printable.</summary>
    /// <param name="text">The original text, if present.</param>
    /// <returns>The safe single-line text.</returns>
    public static string OneLine(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : PrintableText.OneLine(text.ReplaceLineEndings(LineBreak));

    /// <summary>Formats a value for display according to the caller's masking decision.</summary>
    /// <param name="value">The original value, if present.</param>
    /// <param name="masked">Whether to hide the value.</param>
    /// <returns>Empty text for an absent value, <see cref="Mask"/> for a masked value, or the printable value.</returns>
    public static string Value(string? value, bool masked)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return masked ? Mask : OneLine(value);
    }
}
