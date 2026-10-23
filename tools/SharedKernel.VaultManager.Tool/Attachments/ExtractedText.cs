using System.Text;

namespace PandaBear.VaultManager.Tool.Attachments;

/// <summary>Text read from an attachment within the extraction limits.</summary>
/// <param name="Text">Extracted text.</param>
/// <param name="Truncated">Whether the file was longer than the byte limit and its end was dropped.</param>
/// <param name="TruncatedLines">Number of lines cut at the line length limit.</param>
internal sealed record ExtractedText(string Text, bool Truncated, int TruncatedLines)
{
    /// <summary>Prints only the shape, so attachment content never reaches logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Length = ").Append(Text.Length).Append(", Truncated = ").Append(Truncated)
            .Append(", TruncatedLines = ").Append(TruncatedLines);
        return true;
    }
}
