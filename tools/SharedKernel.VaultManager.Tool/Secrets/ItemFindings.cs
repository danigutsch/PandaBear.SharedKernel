namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Findings of one item, for reporting.</summary>
/// <param name="ItemName">Item name.</param>
/// <param name="Findings">Findings of the item.</param>
internal sealed record ItemFindings(string ItemName, IReadOnlyList<SecretFinding> Findings);
