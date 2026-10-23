namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Changed fields of one merged group.</summary>
/// <param name="GroupKey">Group key.</param>
/// <param name="MemberNames">Names of the group members.</param>
/// <param name="Fields">Changed field paths and how each was resolved.</param>
internal sealed record GroupReport(string GroupKey, IReadOnlyList<string> MemberNames, IReadOnlyList<FieldResolution> Fields);
