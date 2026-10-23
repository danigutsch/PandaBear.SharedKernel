using System.Text;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>Input item with its secret findings.</summary>
/// <param name="Source">Item as loaded.</param>
/// <param name="Findings">Findings in the item and in its text attachments stored in the input archive.</param>
/// <param name="AttachmentTexts">
/// Text of each scanned attachment by its <c>attachment:</c> field path. Placement reads finding values from it.
/// </param>
internal sealed record ScannedItem(SourceItem Source, IReadOnlyList<SecretFinding> Findings, IReadOnlyDictionary<string, string> AttachmentTexts)
{
    /// <summary>Prints the item and counts only, so attachment text never reaches logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Source = ").Append(Source).Append(", Findings = ").Append(Findings.Count)
            .Append(", AttachmentTexts = ").Append(AttachmentTexts.Count);
        return true;
    }
}
