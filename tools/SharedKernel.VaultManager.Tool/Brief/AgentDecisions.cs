using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// Limits and combines the decisions file that an agent writes from a brief. An agent may only take one member's value for
/// a field the brief lets it decide, which keeps the other values as a merge keeps every losing value, and accept or reject
/// the findings the brief lets it decide. It may not drop values, change the matching, or decide which notes are shown.
/// </summary>
internal static class AgentDecisions
{
    /// <summary>
    /// Finds what agent decisions may not decide: an <see cref="FieldChoiceKind.Empty"/> choice, a link, a split, a notes
    /// decision, a second decision on one field or finding, a group, field, member, item, or finding that the brief does not
    /// list for agent decisions, or an accept or reject that the brief does not allow. Pending entries are checked like
    /// decisions.
    /// </summary>
    /// <param name="agent">Agent decisions.</param>
    /// <param name="brief">Brief the agent read.</param>
    /// <returns>One problem per decision outside the limits, naming keys, field paths, and finding IDs, never values.</returns>
    public static IReadOnlyList<string> Check(MergeDecisions agent, BriefDocument brief)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(brief);

        var problems = new List<string>();
        if (agent.Overrides.Links.Count > 0)
        {
            problems.Add("links are not allowed: only you decide which items are one account.");
        }

        if (agent.Overrides.Splits.Count > 0)
        {
            problems.Add("splits are not allowed: only you decide which items are one account.");
        }

        if (agent.Notes.Count > 0)
        {
            problems.Add("notes decisions are not allowed: only you decide which notes an agent may read.");
        }

        Dictionary<string, BriefGroup> groups = brief.Groups.ToDictionary(group => group.Key, StringComparer.Ordinal);
        var decidedFields = new HashSet<(string GroupKey, string FieldPath)>();
        foreach (GroupDecision decision in agent.Groups)
        {
            if (!groups.TryGetValue(decision.GroupKey, out BriefGroup? group))
            {
                problems.Add($"group {decision.GroupKey} is not in the brief.");
                continue;
            }

            foreach (FieldChoice choice in decision.Choices)
            {
                problems.AddRange(decidedFields.Add((decision.GroupKey, choice.FieldPath))
                    ? CheckChoice(choice, group)
                    : [$"the agent decisions file decides field {choice.FieldPath} of group {decision.GroupKey} more than once."]);
            }
        }

        var findings = new Dictionary<string, Dictionary<string, BriefFinding>>(StringComparer.Ordinal);
        foreach (BriefMember member in brief.Groups.SelectMany(group => group.Members).Where(member => member.Findings.Count > 0))
        {
            findings.TryAdd(member.OriginKey, member.Findings.ToDictionary(finding => finding.Id, StringComparer.Ordinal));
        }

        var decidedFindings = new HashSet<(string OriginKey, string FindingId)>();
        foreach (ItemFindingDecisions item in agent.Findings)
        {
            if (!findings.TryGetValue(item.OriginKey, out Dictionary<string, BriefFinding>? listed))
            {
                problems.Add($"finding decisions for {item.OriginKey} name an item that is not in the brief.");
                continue;
            }

            foreach (FindingChoice choice in item.Choices)
            {
                problems.AddRange(decidedFindings.Add((item.OriginKey, choice.FindingId))
                    ? CheckFinding(choice, item.OriginKey, listed.GetValueOrDefault(choice.FindingId))
                    : [$"the agent decisions file decides finding {choice.FindingId} of {item.OriginKey} more than once."]);
            }
        }

        return problems;
    }

    /// <summary>
    /// Combines the user's and the agent's decisions. The user's field and finding decisions come first, so on the same
    /// field or finding the user's decision wins; links, splits, and notes decisions are the user's only.
    /// </summary>
    /// <param name="user">User decisions.</param>
    /// <param name="agent">Agent decisions that <see cref="Check"/> found within the limits.</param>
    /// <returns>The combined decisions.</returns>
    public static MergeDecisions Combine(MergeDecisions user, MergeDecisions agent)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(agent);

        return user with
        {
            Groups = [.. user.Groups, .. agent.Groups],
            Findings = [.. user.Findings, .. agent.Findings],
        };
    }

    /// <summary>The agent's decisions that take effect: every one that is not pending and on which the user has no decision.</summary>
    /// <param name="user">User decisions.</param>
    /// <param name="agent">Agent decisions.</param>
    /// <returns>The effective field and finding decisions of the agent.</returns>
    public static MergeDecisions Effective(MergeDecisions user, MergeDecisions agent)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(agent);

        GroupDecision[] groups =
        [
            .. agent.Groups
                .Select(decision => decision with
                {
                    Choices = [.. decision.Choices.Where(choice => choice.Kind != FieldChoiceKind.Pending && user.Find(decision.GroupKey, choice.FieldPath) is null)],
                })
                .Where(decision => decision.Choices.Count > 0),
        ];
        ItemFindingDecisions[] findings =
        [
            .. agent.Findings
                .Select(item => item with
                {
                    Choices = [.. item.Choices.Where(choice => choice.Decision != FindingDecision.Pending && user.FindFinding(item.OriginKey, choice.FindingId) is null)],
                })
                .Where(item => item.Choices.Count > 0),
        ];
        return new MergeDecisions(groups, MatchOverrides.None) { Findings = findings };
    }

    /// <summary>
    /// Finds the pre-accepted findings that the agent rejects and the user has not decided, so the rejection takes effect
    /// and the merge lists it.
    /// </summary>
    /// <param name="user">User decisions.</param>
    /// <param name="agent">Agent decisions.</param>
    /// <param name="findings">Findings of the merge, per input item.</param>
    /// <returns>Per item with such findings, its origin key, name, and the rejected findings.</returns>
    public static IReadOnlyList<ItemFindingDecisions> RejectedByAgent(MergeDecisions user, MergeDecisions agent, IReadOnlyList<ItemFindingsResult> findings)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(findings);

        var rejected = new List<ItemFindingDecisions>();
        foreach (ItemFindingsResult item in findings)
        {
            FindingChoice[] choices =
            [
                .. item.Findings
                    .Where(finding => finding.IsPreAccepted
                        && user.FindFinding(item.OriginKey, finding.Id) is null
                        && agent.FindFinding(item.OriginKey, finding.Id) == FindingDecision.Rejected)
                    .Select(finding => new FindingChoice(finding.Id, FindingDecision.Rejected)),
            ];
            if (choices.Length > 0)
            {
                rejected.Add(new ItemFindingDecisions(item.OriginKey, choices, item.ItemName));
            }
        }

        return rejected;
    }

    /// <summary>
    /// Finds what the agent's finding decisions change beyond the brief. Placement runs before matching, so a finding
    /// decision could put an item in a group with other items, which only links and splits may do, or empty the value that a
    /// member choice takes.
    /// </summary>
    /// <param name="userOnly">Merge with the user's decisions only, which the brief describes.</param>
    /// <param name="applied">Merge with the user's and the agent's decisions.</param>
    /// <param name="decisions">The user's and the agent's decisions, combined by <see cref="Combine"/>.</param>
    /// <returns>One problem per changed group or member choice, naming keys and field paths, never values.</returns>
    public static IReadOnlyList<string> Conflicts(PipelineResult userOnly, PipelineResult applied, MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(userOnly);
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(decisions);

        var problems = new List<string>();
        Dictionary<string, string> before = MembersByItem(userOnly);
        Dictionary<string, string> after = MembersByItem(applied);
        foreach (MatchGroup group in applied.Match.Groups)
        {
            if (group.MemberKeys.Any(key => before.TryGetValue(key, out string? members) && !string.Equals(members, after[key], StringComparison.Ordinal)))
            {
                problems.Add($"the agent's finding decisions put {string.Join(", ", group.MemberKeys)} in group {group.Key}, which the brief does not group this way.");
            }
        }

        foreach (GroupDecision decision in decisions.Groups)
        {
            foreach (FieldChoice choice in decision.Choices.Where(choice => choice.Kind == FieldChoiceKind.Member && decisions.Find(decision.GroupKey, choice.FieldPath) == choice))
            {
                if (Holds(userOnly, decision.GroupKey, choice) && !Holds(applied, decision.GroupKey, choice))
                {
                    problems.Add($"the choice for {choice.FieldPath} in group {decision.GroupKey} names {choice.OriginKey}, which holds no value for it once the agent's finding decisions are placed.");
                }
            }
        }

        return problems;
    }

    private static IEnumerable<string> CheckChoice(FieldChoice choice, BriefGroup group)
    {
        if (choice.Kind == FieldChoiceKind.Empty)
        {
            yield return $"the choice for {choice.FieldPath} in group {group.Key} is Empty: an agent may only take one member's value, which keeps the other values.";
            yield break;
        }

        BriefField? field = group.Fields.FirstOrDefault(field => field.AgentMayDecide && string.Equals(field.FieldPath, choice.FieldPath, StringComparison.Ordinal));
        if (field is null)
        {
            yield return $"field {choice.FieldPath} of group {group.Key} is not one that the brief lets an agent decide.";
        }
        else if (choice.Kind == FieldChoiceKind.Member && !field.Holders.Any(holder => string.Equals(holder.OriginKey, choice.OriginKey, StringComparison.Ordinal)))
        {
            yield return $"the choice for {choice.FieldPath} in group {group.Key} names {choice.OriginKey}, which holds no value for it in the brief.";
        }
    }

    private static IEnumerable<string> CheckFinding(FindingChoice choice, string originKey, BriefFinding? finding)
    {
        if (finding is null)
        {
            yield return $"finding {choice.FindingId} of {originKey} is not in the brief.";
        }
        else if (choice.Decision == FindingDecision.Accepted && !finding.AgentMayAccept)
        {
            yield return $"finding {choice.FindingId} of {originKey} may not be accepted by an agent: it needs review, moves text into a place that catalogs and briefs show, or changes which items match.";
        }
        else if (choice.Decision == FindingDecision.Rejected && !finding.AgentMayReject)
        {
            yield return $"finding {choice.FindingId} of {originKey} may not be rejected by an agent: it is pre-accepted, and rejecting it changes which items match.";
        }
    }

    private static Dictionary<string, string> MembersByItem(PipelineResult result)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (MatchGroup group in result.Match.Groups)
        {
            string keys = string.Join('\n', group.MemberKeys.Order(StringComparer.Ordinal));
            foreach (string key in group.MemberKeys)
            {
                members.TryAdd(key, keys);
            }
        }

        return members;
    }

    private static bool Holds(PipelineResult result, string groupKey, FieldChoice choice)
    {
        MatchGroup? group = result.Match.Groups.FirstOrDefault(candidate => string.Equals(candidate.Key, groupKey, StringComparison.Ordinal));
        int index = group is null ? -1 : group.MemberKeys.ToList().FindIndex(key => string.Equals(key, choice.OriginKey, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        VaultItem member = group!.Members[index];
        return choice.FieldPath switch
        {
            FieldPaths.Name => true,
            FieldPaths.Folder => !string.IsNullOrWhiteSpace(member.FolderPath),
            _ => ScalarFields.All.FirstOrDefault(field => string.Equals(field.Path, choice.FieldPath, StringComparison.Ordinal)) is { } scalar
                && FieldDiff.ScalarKey(scalar, member).Length > 0,
        };
    }
}
