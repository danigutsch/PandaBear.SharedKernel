using PandaBear.VaultManager.Tool.Matching;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>User decisions that steer matching and merging. Holds keys and choices, never values.</summary>
/// <param name="Groups">Field decisions per group.</param>
/// <param name="Overrides">Forced links and splits.</param>
internal sealed record MergeDecisions(IReadOnlyList<GroupDecision> Groups, MatchOverrides Overrides)
{
    /// <summary>No decisions.</summary>
    public static MergeDecisions None { get; } = new([], MatchOverrides.None);

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
}
