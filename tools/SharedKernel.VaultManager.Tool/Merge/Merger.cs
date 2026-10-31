using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Merges match groups field by field. A merge never replaces a whole item and never drops a non-empty value unless a
/// decision chooses <see cref="FieldChoiceKind.Empty"/>.
/// </summary>
/// <remarks>
/// Rules, in order:
/// <list type="number">
/// <item>Values equal after normalization are kept once.</item>
/// <item>A value present on only some members is taken. Null, empty, and whitespace count as absent. Favorite and reprompt are true when any member has them.</item>
/// <item>Addresses, custom fields, passkeys, and password history are unioned. History is ordered newest first.</item>
/// <item>
/// Different values: the policy picks a winner. The loser is kept: a password goes to the history, notes that fit go to a
/// hidden field and longer notes are appended to the winner's notes, and other values become hidden fields named
/// <c>"&lt;Label&gt; (from &lt;source&gt;)"</c>. Missing or tied timestamps under newest-wins, and every conflict under
/// ask, stay unresolved with the newest value, or the first member's value, as a provisional winner.
/// </item>
/// <item>An explicit decision overrides rules 1 to 4.</item>
/// <item>
/// Name, type, and origin come from the newest member, and other names become text fields. The folder is a single value
/// under rules 2, 4, and 5; a folder that is not used is listed in the <see cref="AdditionalFolders"/> field.
/// </item>
/// <item>
/// Every attachment of every member is kept. Copies on several members are kept once, from the newest member, only when
/// their names and content hashes are equal (<see cref="GroupAttachments"/>).
/// </item>
/// </list>
/// </remarks>
/// <param name="timeProvider">Clock for preserved passwords when no member has a timestamp.</param>
internal sealed class Merger(TimeProvider timeProvider)
{
    /// <summary>Merges groups into one item each.</summary>
    /// <param name="groups">Groups from <see cref="DuplicateMatcher"/>.</param>
    /// <param name="policy">Conflict policy.</param>
    /// <param name="decisions">User decisions, or <see langword="null"/> for none.</param>
    /// <returns>The merged export, the unresolved conflicts, and a value-free report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="groups"/> or <paramref name="policy"/> is <see langword="null"/>.</exception>
    public MergeResult Merge(IReadOnlyList<MatchGroup> groups, MergePolicy policy, MergeDecisions? decisions)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(policy);
        decisions ??= MergeDecisions.None;

        var now = timeProvider.GetUtcNow();
        var items = new List<VaultItem>(groups.Count);
        var unresolved = new List<UnresolvedConflict>();
        var reports = new List<GroupReport>();
        int autoResolved = 0;
        int merged = 0;

        foreach (var group in groups)
        {
            if (group.Members.Count == 1)
            {
                items.Add(group.Members[0]);
                continue;
            }

            var merger = new GroupMerger(group, policy, decisions, now);
            items.Add(merger.Merge());
            merged++;
            unresolved.AddRange(merger.Unresolved);
            autoResolved += merger.AutoResolved;
            if (merger.Resolutions.Count > 0)
            {
                reports.Add(new GroupReport(group.Key, [.. group.Members.Select(member => member.Name)], merger.Resolutions));
            }
        }

        var report = new MergeReport
        {
            UniqueGroups = groups.Count(group => group.Status == MatchStatus.Unique),
            IdenticalGroups = groups.Count(group => group.Status == MatchStatus.Identical),
            DifferentGroups = groups.Count(group => group.Status == MatchStatus.Different),
            MergedItems = merged,
            AutoResolvedConflicts = autoResolved,
            UnresolvedConflicts = unresolved.Count,
            Groups = reports,
            ConversionNotes = [.. items.SelectMany(item => item.ConversionNotes.Select(note => item.Name + ": " + note))],
        };
        return new MergeResult(new VaultExport(items), unresolved, report);
    }
}
