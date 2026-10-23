namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>A password weakness of one item. Holds names and counts only, never values.</summary>
/// <param name="ItemName">Name of the affected item.</param>
/// <param name="Kind">Weakness.</param>
/// <param name="RelatedItemNames">
/// For <see cref="PasswordIssueKind.Reused"/>, the names of the first other items on different hosts that use the same
/// password, in item order, at most <see cref="PasswordHealthReport.MaxRelatedItemNames"/>.
/// </param>
/// <param name="RelatedItemCount">
/// For <see cref="PasswordIssueKind.Reused"/>, the number of other items on different hosts that use the same password,
/// listed or not.
/// </param>
internal sealed record PasswordIssue(string ItemName, PasswordIssueKind Kind, IReadOnlyList<string> RelatedItemNames, int RelatedItemCount);
