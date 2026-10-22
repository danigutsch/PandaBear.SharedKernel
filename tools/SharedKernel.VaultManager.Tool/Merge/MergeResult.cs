using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Result of a merge.</summary>
/// <param name="Merged">One item per group. Unresolved fields hold a provisional value and keep every other value.</param>
/// <param name="Unresolved">Fields that need a user decision.</param>
/// <param name="Report">Value-free summary.</param>
internal sealed record MergeResult(VaultExport Merged, IReadOnlyList<UnresolvedConflict> Unresolved, MergeReport Report);
