using System.Text;

namespace PandaBear.VaultManager.Tool.Formats.Csv;

/// <summary>One CSV record.</summary>
/// <param name="LineNumber">One-based line on which the record starts.</param>
/// <param name="Fields">Field values, unquoted.</param>
internal sealed record CsvRecord(int LineNumber, IReadOnlyList<string> Fields)
{
    /// <summary>Prints only the position and shape, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("LineNumber = ").Append(LineNumber).Append(", Fields = ").Append(Fields.Count);
        return true;
    }
}
