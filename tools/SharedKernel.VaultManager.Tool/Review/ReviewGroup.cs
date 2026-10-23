using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>A match group as the review lists it. Holds names, keys, field paths, and counts, never values.</summary>
/// <param name="Key">Group key.</param>
/// <param name="Name">Name of the merged item.</param>
/// <param name="Type">Type of the merged item.</param>
/// <param name="Status">How the members relate.</param>
/// <param name="MemberKeys">Origin keys of the members, in group order.</param>
/// <param name="MemberNames">Names of the members, aligned with <paramref name="MemberKeys"/>.</param>
/// <param name="ConflictingFields">
/// Field paths whose members hold different values and that a member choice decides: the name, the folder, and the
/// single-value fields with at least two different present values.
/// </param>
/// <param name="PendingFields">Fields that need a decision.</param>
/// <param name="Findings">Secret findings of the members.</param>
/// <param name="PendingFindings">Proposed findings of the members that are neither accepted nor rejected.</param>
/// <param name="IsSuggested">Whether a weak match suggests joining a member with an item of another group.</param>
internal sealed record ReviewGroup(
    string Key,
    string Name,
    VaultItemType Type,
    MatchStatus Status,
    IReadOnlyList<string> MemberKeys,
    IReadOnlyList<string> MemberNames,
    IReadOnlyList<string> ConflictingFields,
    int PendingFields,
    int Findings,
    int PendingFindings,
    bool IsSuggested)
{
    /// <summary>Whether a field or a finding of the group needs a decision before the merged vault can be written.</summary>
    public bool IsPending => PendingFields > 0 || PendingFindings > 0;
}
