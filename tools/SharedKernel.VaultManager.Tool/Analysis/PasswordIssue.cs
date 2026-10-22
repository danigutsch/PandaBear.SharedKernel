namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>A password weakness of one item. Holds names only, never values.</summary>
/// <param name="ItemName">Name of the affected item.</param>
/// <param name="Kind">Weakness.</param>
/// <param name="RelatedItemNames">For <see cref="PasswordIssueKind.Reused"/>, the names of the other items on different hosts.</param>
internal sealed record PasswordIssue(string ItemName, PasswordIssueKind Kind, IReadOnlyList<string> RelatedItemNames);
