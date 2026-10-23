namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>A field on which the members of a group differ.</summary>
/// <param name="FieldPath">Field path, as decisions name it.</param>
/// <param name="NeedsDecision">Whether the policy leaves the field to a decision.</param>
/// <param name="AgentMayDecide">
/// Whether an agent decisions file may take one member's value for the field: the name, the folder, and the single-value
/// fields of the merged item's type. Lists take only decisions that drop values, which agents may not make.
/// </param>
/// <param name="Holders">Members that hold a value for the field, in group order.</param>
internal sealed record BriefField(string FieldPath, bool NeedsDecision, bool AgentMayDecide, IReadOnlyList<BriefHolder> Holders);
