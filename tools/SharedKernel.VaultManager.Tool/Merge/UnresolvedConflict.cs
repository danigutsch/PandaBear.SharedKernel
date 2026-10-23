namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>A field whose winner needs a user decision. Holds no values.</summary>
/// <param name="GroupKey">Group key.</param>
/// <param name="FieldPath">Field path.</param>
/// <param name="MemberOriginKeys">Origin keys of every group member, in group order.</param>
/// <param name="MemberNames">Names of every group member, aligned with <paramref name="MemberOriginKeys"/>.</param>
internal sealed record UnresolvedConflict(string GroupKey, string FieldPath, IReadOnlyList<string> MemberOriginKeys, IReadOnlyList<string> MemberNames);
