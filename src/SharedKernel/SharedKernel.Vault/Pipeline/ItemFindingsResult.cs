using PandaBear.Vault.Secrets;

namespace PandaBear.Vault.Pipeline;

/// <summary>Secret findings of one input item and the ones that were accepted.</summary>
/// <param name="OriginKey">Origin key of the item; finding decisions name the item by it.</param>
/// <param name="ItemName">Item name as read.</param>
/// <param name="Findings">Findings in the item and in its text attachments stored in the input archive.</param>
/// <param name="AcceptedFindingIds">
/// Identifiers of the findings passed to placement, chosen by <see cref="Merge.MergeDecisions.Applies"/>. Findings that
/// placement could not move are listed in <see cref="PipelineResult.PlacementProblems"/>.
/// </param>
public sealed record ItemFindingsResult(string OriginKey, string ItemName, IReadOnlyList<SecretFinding> Findings, IReadOnlyList<string> AcceptedFindingIds);
