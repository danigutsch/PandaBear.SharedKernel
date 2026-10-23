using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>File attached to a vault item in the source vault.</summary>
/// <param name="FileName">Attachment file name.</param>
/// <param name="Size">Size in bytes, when known.</param>
/// <param name="SourcePath">Path inside the source export archive, when the file is included.</param>
internal sealed record AttachmentRef(string FileName, long? Size, string? SourcePath)
{
    /// <summary>
    /// Lowercase hexadecimal SHA-256 of the file, set when the scan read the file from the source export archive, or
    /// <see langword="null"/> when the content is unknown. Two attachments with the same name and hash hold the same file.
    /// </summary>
    public string? ContentHash
    {
        get; init;
    }

    /// <summary>
    /// Prints the name, size, and archive path only, so a digest that could confirm a guessed file content never reaches
    /// logs or messages through <c>ToString</c>.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("FileName = ").Append(FileName)
            .Append(", Size = ").Append(Size?.ToString(CultureInfo.InvariantCulture))
            .Append(", SourcePath = ").Append(SourcePath);
        return true;
    }
}
