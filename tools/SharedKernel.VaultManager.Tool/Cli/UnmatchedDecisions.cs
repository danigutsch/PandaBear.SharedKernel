using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// Finds decisions that match nothing in a merge, so a typo in a decisions file is reported instead of silently leaving
/// the policy or a pre-accepted finding in effect. Pending entries are not decisions and are not checked.
/// </summary>
internal static class UnmatchedDecisions
{
    /// <summary>Describes every decision whose group, field, origin key, or finding identifier matches nothing.</summary>
    /// <param name="decisions">Decisions the pipeline ran with.</param>
    /// <param name="result">Pipeline result.</param>
    /// <returns>One message per unmatched decision. Messages hold keys, field paths, and identifiers, never values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="decisions"/> or <paramref name="result"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Find(MergeDecisions decisions, PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(result);

        var warnings = new List<string>();
        AddGroupWarnings(warnings, decisions.Groups, result);
        HashSet<string> originKeys = [.. result.Match.Groups.SelectMany(group => group.MemberKeys)];
        AddOverrideWarnings(warnings, "link", decisions.Overrides.Links, originKeys);
        AddOverrideWarnings(warnings, "split", decisions.Overrides.Splits, originKeys);
        AddFindingWarnings(warnings, decisions.Findings, result.Findings);
        return warnings;
    }

    private static void AddGroupWarnings(List<string> warnings, IReadOnlyList<GroupDecision> decisions, PipelineResult result)
    {
        // Field decisions apply to groups with more than one member; the merged item tells which fields its type has.
        var merged = new Dictionary<string, (MatchGroup Group, VaultItem Item)>(StringComparer.Ordinal);
        for (int index = 0; index < result.Match.Groups.Count; index++)
        {
            MatchGroup group = result.Match.Groups[index];
            if (group.Members.Count > 1)
            {
                merged.TryAdd(group.Key, (group, result.Merged.Items[index]));
            }
        }

        foreach (GroupDecision decision in decisions)
        {
            FieldChoice[] choices = [.. decision.Choices.Where(choice => choice.Kind != FieldChoiceKind.Pending)];
            if (choices.Length == 0)
            {
                continue;
            }

            if (!merged.TryGetValue(decision.GroupKey, out var target))
            {
                warnings.Add($"decisions for group {decision.GroupKey} match no merged group and are ignored.");
                continue;
            }

            foreach (FieldChoice choice in choices)
            {
                if (!TakesDecision(choice, target.Item.Type))
                {
                    warnings.Add($"the decision on field {choice.FieldPath} of group {decision.GroupKey} matches no field that takes {choice.Kind} decisions and is ignored.");
                }
                else if (choice.Kind == FieldChoiceKind.Member && !target.Group.MemberKeys.Contains(choice.OriginKey, StringComparer.Ordinal))
                {
                    warnings.Add($"the decision on field {choice.FieldPath} of group {decision.GroupKey} names {choice.OriginKey}, which is not in the group, and is ignored. Members: {string.Join(", ", target.Group.MemberKeys)}.");
                }
            }
        }
    }

    // A member choice applies to the name, the folder, and the single-value fields of the merged item's type; an empty
    // choice applies to those single-value fields and to the unioned collections.
    private static bool TakesDecision(FieldChoice choice, VaultItemType type)
    {
        bool singleValue = ScalarFields.All.Any(field => string.Equals(field.Path, choice.FieldPath, StringComparison.Ordinal) && field.AppliesTo(type));
        return choice.Kind == FieldChoiceKind.Member
            ? singleValue || choice.FieldPath is FieldPaths.Name or FieldPaths.Folder
            : singleValue || choice.FieldPath is FieldPaths.Fields or FieldPaths.PasswordHistory or FieldPaths.LoginUris or FieldPaths.LoginFido2Credentials;
    }

    private static void AddOverrideWarnings(List<string> warnings, string kind, IReadOnlyList<OriginKeyPair> pairs, HashSet<string> originKeys)
    {
        foreach (OriginKeyPair pair in pairs)
        {
            foreach (string originKey in new[] { pair.First, pair.Second }.Distinct(StringComparer.Ordinal).Where(key => !originKeys.Contains(key)))
            {
                warnings.Add($"the {kind} between {pair.First} and {pair.Second} names {originKey}, which matches no item, and is ignored.");
            }
        }
    }

    private static void AddFindingWarnings(List<string> warnings, IReadOnlyList<ItemFindingDecisions> decisions, IReadOnlyList<ItemFindingsResult> findings)
    {
        var findingIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (ItemFindingsResult item in findings)
        {
            findingIds.TryAdd(item.OriginKey, [.. item.Findings.Select(finding => finding.Id)]);
        }

        foreach (ItemFindingDecisions item in decisions)
        {
            FindingChoice[] choices = [.. item.Choices.Where(choice => choice.Decision != FindingDecision.Pending)];
            if (choices.Length == 0)
            {
                continue;
            }

            if (!findingIds.TryGetValue(item.OriginKey, out HashSet<string>? ids))
            {
                warnings.Add($"finding decisions for {item.OriginKey} match no item with findings and are ignored.");
                continue;
            }

            foreach (FindingChoice choice in choices.Where(choice => !ids.Contains(choice.FindingId)))
            {
                warnings.Add($"the decision on finding {choice.FindingId} of {item.OriginKey} matches no finding of the item and is ignored.");
            }
        }
    }
}
