using System.Text;
using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// One field of a match group, with the value of every member side by side. Values are display text: secret values show
/// as <see cref="ReviewText.Mask"/> until the field is revealed.
/// </summary>
/// <param name="FieldPath">Field path, as decisions name it.</param>
/// <param name="Label">Human label of the field.</param>
/// <param name="Values">Display text of each member's value, aligned with the group members.</param>
/// <param name="IsSecret">Whether a value of the field is masked unless the field is revealed.</param>
/// <param name="IsRevealed">Whether the field is revealed, so its secret values show.</param>
/// <param name="Differs">Whether the members hold different values.</param>
/// <param name="IsConflict">Whether the members hold different values that a member choice decides.</param>
/// <param name="Choice">Decision on the field, or <see langword="null"/> when there is none.</param>
/// <param name="Resolution">How the latest merge resolved the field, or <see langword="null"/> when no member changed it.</param>
/// <param name="AcceptsMember">Whether a decision can take one member's value.</param>
/// <param name="AcceptsEmpty">Whether a decision can drop the field from every member.</param>
internal sealed record ReviewField(
    string FieldPath,
    string Label,
    IReadOnlyList<string> Values,
    bool IsSecret,
    bool IsRevealed,
    bool Differs,
    bool IsConflict,
    FieldChoice? Choice,
    ResolutionKind? Resolution,
    bool AcceptsMember,
    bool AcceptsEmpty)
{
    /// <summary>Prints the field path, flags, and the decision only, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("FieldPath = ").Append(FieldPath)
            .Append(", Values = ").Append(Values.Count)
            .Append(", IsSecret = ").Append(IsSecret)
            .Append(", IsRevealed = ").Append(IsRevealed)
            .Append(", Differs = ").Append(Differs)
            .Append(", Choice = ").Append(Choice?.Kind)
            .Append(", Resolution = ").Append(Resolution);
        return true;
    }
}
