namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Conflict policy of a merge.</summary>
/// <param name="Kind">How a winner is picked.</param>
/// <param name="PreferredSourceFile">
/// Source file name whose values win under <see cref="MergePolicyKind.PreferSource"/>. When that file holds no value
/// for a field, the newest-wins rule applies to that field.
/// </param>
internal sealed record MergePolicy(MergePolicyKind Kind = MergePolicyKind.NewestWins, string? PreferredSourceFile = null);
