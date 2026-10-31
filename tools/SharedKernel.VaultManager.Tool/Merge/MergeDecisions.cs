using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// User decisions that steer secret placement, matching, and merging. Holds keys and choices, never values. Pending
/// entries mark fields and findings that still need a decision; they count as no decision.
/// </summary>
/// <param name="Groups">Field decisions per group.</param>
/// <param name="Overrides">Forced links and splits.</param>
internal sealed record MergeDecisions(IReadOnlyList<GroupDecision> Groups, MatchOverrides Overrides)
{
    /// <summary>No decisions.</summary>
    public static MergeDecisions None { get; } = new([], MatchOverrides.None);

    /// <summary>Decisions on secret findings, per input item.</summary>
    public IReadOnlyList<ItemFindingDecisions> Findings { get; init; } = [];

    /// <summary>Decisions on the notes of catalog items, at most one per input item.</summary>
    public IReadOnlyList<ItemNotesDecision> Notes { get; init; } = [];

    /// <summary>Finds the entry on the notes of an input item.</summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <returns>The entry, or <see langword="null"/> when there is none.</returns>
    public ItemNotesDecision? FindNotesEntry(string originKey) =>
        Notes.FirstOrDefault(item => string.Equals(item.OriginKey, originKey, StringComparison.Ordinal));

    /// <summary>Finds the decision on the notes of an input item.</summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <returns>The decision; <see cref="NotesDecision.Pending"/> when there is none.</returns>
    public NotesDecision FindNotes(string originKey) => FindNotesEntry(originKey)?.Decision ?? NotesDecision.Pending;

    /// <summary>
    /// Whether the notes of an input item are shared: the decision is <see cref="NotesDecision.Share"/> and was made for
    /// exactly these notes (<see cref="ItemNotesDecision.NotesSha256"/>).
    /// </summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <param name="notes">The item's current notes.</param>
    /// <returns><see langword="true"/> when the notes may be shown and edited.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notes"/> is <see langword="null"/>.</exception>
    public bool SharesNotes(string originKey, string notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return FindNotesEntry(originKey) is { Decision: NotesDecision.Share, NotesSha256: { } hash }
            && string.Equals(hash, ItemNotesDecision.HashOf(notes), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns these decisions with notes entries updated: every pending entry is dropped, each given entry replaces the
    /// entry of its item in place or is added, and other entries stay as they are.
    /// </summary>
    /// <param name="updates">New entries, such as pending entries for notes that need a decision.</param>
    /// <returns>The updated decisions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="updates"/> is <see langword="null"/>.</exception>
    public MergeDecisions WithNotes(IEnumerable<ItemNotesDecision> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        Dictionary<string, ItemNotesDecision> byKey = updates.ToDictionary(item => item.OriginKey, StringComparer.Ordinal);
        var notes = new List<ItemNotesDecision>();
        foreach (ItemNotesDecision entry in Notes)
        {
            if (byKey.Remove(entry.OriginKey, out ItemNotesDecision? update))
            {
                notes.Add(update);
            }
            else if (entry.Decision != NotesDecision.Pending)
            {
                notes.Add(entry);
            }
        }

        return this with
        {
            Notes = [.. notes, .. updates.Where(item => byKey.ContainsKey(item.OriginKey))]
        };
    }

    /// <summary>Finds the decision for a field of a group. The first matching decision counts; pending entries are skipped.</summary>
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
                if (choice.Kind != FieldChoiceKind.Pending && string.Equals(choice.FieldPath, fieldPath, StringComparison.Ordinal))
                {
                    return choice;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the decision on a finding of an input item. The first matching decision counts; pending entries are skipped.
    /// </summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <param name="findingId">Finding identifier.</param>
    /// <returns><see cref="FindingDecision.Accepted"/>, <see cref="FindingDecision.Rejected"/>, or <see langword="null"/> when there is none.</returns>
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
                if (choice.Decision != FindingDecision.Pending && string.Equals(choice.FindingId, findingId, StringComparison.Ordinal))
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="finding"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Returns these decisions with their pending entries replaced: every pending entry is dropped, and one is added for
    /// each given field and finding without a decision. A <see cref="FieldChoiceKind.Member"/> choice that names no
    /// member of its field's group is no decision, because the merge ignores it; the field's pending entry takes its
    /// place. Other decisions and match overrides stay as they are, and groups and items left without entries are
    /// dropped.
    /// </summary>
    /// <param name="fields">Fields that need a decision. The entry lists their members as options.</param>
    /// <param name="findings">Findings that need a decision, per item; their decisions are ignored.</param>
    /// <returns>The updated decisions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> or <paramref name="findings"/> is <see langword="null"/>.</exception>
    public MergeDecisions WithPending(IReadOnlyList<UnresolvedConflict> fields, IReadOnlyList<ItemFindingDecisions> findings)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(findings);

        List<GroupDecision> groups = [.. Groups.Select(group => group with { Choices = [.. group.Choices.Where(choice => choice.Kind != FieldChoiceKind.Pending)] })];
        foreach (UnresolvedConflict conflict in fields)
        {
            FieldChoice? decision = Find(conflict.GroupKey, conflict.FieldPath);
            if (decision is null)
            {
                AddPending(groups, conflict);
            }
            else if (decision.Kind == FieldChoiceKind.Member && !conflict.MemberOriginKeys.Contains(decision.OriginKey, StringComparer.Ordinal))
            {
                ReplaceWithPending(groups, decision, conflict);
            }
        }

        List<ItemFindingDecisions> items = [.. Findings.Select(item => item with { Choices = [.. item.Choices.Where(choice => choice.Decision != FindingDecision.Pending)] })];
        foreach (ItemFindingDecisions item in findings)
        {
            foreach (FindingChoice choice in item.Choices)
            {
                if (FindFinding(item.OriginKey, choice.FindingId) is null)
                {
                    AddPending(items, item, choice.FindingId);
                }
            }
        }

        return this with
        {
            Groups = [.. groups.Where(group => group.Choices.Count > 0)],
            Findings = [.. items.Where(item => item.Choices.Count > 0)],
        };
    }

    private static FieldChoice PendingEntry(UnresolvedConflict conflict) =>
        FieldChoice.Pending(conflict.FieldPath, [.. conflict.MemberOriginKeys.Select((originKey, index) => new FieldChoiceOption(originKey, conflict.MemberNames[index]))]);

    // The choice is replaced where Find found it, the first decision on the field, so the decision the user writes into
    // the pending entry is the one that counts.
    private static void ReplaceWithPending(List<GroupDecision> groups, FieldChoice ignored, UnresolvedConflict conflict)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            GroupDecision group = groups[index];
            int position = string.Equals(group.GroupKey, conflict.GroupKey, StringComparison.Ordinal) ? IndexOf(group.Choices, ignored) : -1;
            if (position >= 0)
            {
                groups[index] = group with
                {
                    Choices = [.. group.Choices.Take(position), PendingEntry(conflict), .. group.Choices.Skip(position + 1)]
                };
                return;
            }
        }
    }

    private static int IndexOf(IReadOnlyList<FieldChoice> choices, FieldChoice choice)
    {
        for (int index = 0; index < choices.Count; index++)
        {
            if (choices[index] == choice)
            {
                return index;
            }
        }

        return -1;
    }

    private static void AddPending(List<GroupDecision> groups, UnresolvedConflict conflict)
    {
        FieldChoice pending = PendingEntry(conflict);
        int index = groups.FindIndex(group => string.Equals(group.GroupKey, conflict.GroupKey, StringComparison.Ordinal));
        if (index < 0)
        {
            groups.Add(new GroupDecision(conflict.GroupKey, [pending]));
        }
        else if (!groups[index].Choices.Any(choice => string.Equals(choice.FieldPath, conflict.FieldPath, StringComparison.Ordinal)))
        {
            groups[index] = groups[index] with
            {
                Choices = [.. groups[index].Choices, pending]
            };
        }
    }

    private static void AddPending(List<ItemFindingDecisions> items, ItemFindingDecisions item, string findingId)
    {
        var pending = new FindingChoice(findingId, FindingDecision.Pending);
        int index = items.FindIndex(existing => string.Equals(existing.OriginKey, item.OriginKey, StringComparison.Ordinal));
        if (index < 0)
        {
            items.Add(new ItemFindingDecisions(item.OriginKey, [pending], item.ItemName));
        }
        else if (!items[index].Choices.Any(choice => string.Equals(choice.FindingId, findingId, StringComparison.Ordinal)))
        {
            items[index] = items[index] with
            {
                Choices = [.. items[index].Choices, pending],
                ItemName = items[index].ItemName ?? item.ItemName
            };
        }
    }
}
