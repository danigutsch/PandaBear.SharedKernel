using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decisions that steer secret placement, matching, and merging. Holds keys and choices, never values.</summary>
/// <param name="Groups">Field decisions per group.</param>
/// <param name="Overrides">Forced links and splits.</param>
internal sealed record MergeDecisions(IReadOnlyList<GroupDecision> Groups, MatchOverrides Overrides)
{
    /// <summary>No decisions.</summary>
    public static MergeDecisions None { get; } = new([], MatchOverrides.None);

    /// <summary>Decisions on secret findings, per input item.</summary>
    public IReadOnlyList<ItemFindingDecisions> Findings { get; init; } = [];

    /// <summary>Finds the decision for a field of a group.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="fieldPath">Field path.</param>
    /// <returns>The decision, or <see langword="null"/> when there is none.</returns>
    public FieldChoice? Find(string groupKey, string fieldPath)
    {
        foreach (var group in Groups)
        {
            if (!string.Equals(group.GroupKey, groupKey, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var choice in group.Choices)
            {
                if (string.Equals(choice.FieldPath, fieldPath, StringComparison.Ordinal))
                {
                    return choice;
                }
            }
        }

        return null;
    }

    /// <summary>Finds the decision on a finding of an input item. The first matching decision counts.</summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <param name="findingId">Finding identifier.</param>
    /// <returns>The decision, or <see langword="null"/> when there is none.</returns>
    public FindingDecision? FindFinding(string originKey, string findingId)
    {
        foreach (var item in Findings)
        {
            if (!string.Equals(item.OriginKey, originKey, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var choice in item.Choices)
            {
                if (string.Equals(choice.FindingId, findingId, StringComparison.Ordinal))
                {
                    return choice.Decision;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a finding is applied: it is accepted here, or it is pre-accepted (<see cref="SecretFinding.IsPreAccepted"/>)
    /// and not rejected here.
    /// </summary>
    /// <param name="originKey">Origin key of the item the finding belongs to.</param>
    /// <param name="finding">Finding.</param>
    /// <returns><see langword="true"/> when the finding is moved to its target.</returns>
    public bool Applies(string originKey, SecretFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return FindFinding(originKey, finding.Id) switch
        {
            FindingDecision.Accepted => true,
            FindingDecision.Rejected => false,
            _ => finding.IsPreAccepted,
        };
    }
}
