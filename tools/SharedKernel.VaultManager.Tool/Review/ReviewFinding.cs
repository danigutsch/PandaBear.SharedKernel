using System.Text;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// A secret finding of a group member as the review shows it. <see cref="Value"/> is <see cref="ReviewText.Mask"/>
/// until the finding is revealed.
/// </summary>
/// <param name="OriginKey">Origin key of the item that holds the finding.</param>
/// <param name="ItemName">Name of that item.</param>
/// <param name="FindingId">Finding identifier, as decisions name it.</param>
/// <param name="Kind">Kind of secret.</param>
/// <param name="Confidence">How certain the finding is.</param>
/// <param name="Target">Where an applied finding moves the secret, described without values.</param>
/// <param name="Message">Description of the finding without its value.</param>
/// <param name="IsPreAccepted">Whether the finding is applied unless a decision rejects it.</param>
/// <param name="NeedsReview">Whether a person must look at the finding before anything happens.</param>
/// <param name="Decision">Accept or reject decision, or <see langword="null"/> when there is none.</param>
/// <param name="IsApplied">Whether the latest merge moved the secret to its target.</param>
/// <param name="Problem">
/// Why the latest merge did not move the secret of an accepted finding, without values, or <see langword="null"/>.
/// </param>
/// <param name="IsRevealed">Whether the finding is revealed, so <see cref="Value"/> shows the secret.</param>
/// <param name="Value">Display text of the secret, or <see cref="ReviewText.Mask"/>.</param>
internal sealed record ReviewFinding(
    string OriginKey,
    string ItemName,
    string FindingId,
    SecretKind Kind,
    Confidence Confidence,
    string Target,
    string Message,
    bool IsPreAccepted,
    bool NeedsReview,
    FindingDecision? Decision,
    bool IsApplied,
    string? Problem,
    bool IsRevealed,
    string Value)
{
    /// <summary>Whether the finding needs an accept or reject decision before the merged vault can be written.</summary>
    public bool IsPending => !IsPreAccepted && Decision is null;

    /// <summary>Prints keys and flags only, so the secret never reaches logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("OriginKey = ").Append(OriginKey)
            .Append(", FindingId = ").Append(FindingId)
            .Append(", Kind = ").Append(Kind)
            .Append(", Decision = ").Append(Decision)
            .Append(", IsApplied = ").Append(IsApplied)
            .Append(", IsRevealed = ").Append(IsRevealed);
        return true;
    }
}
