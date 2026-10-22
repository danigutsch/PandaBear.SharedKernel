namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Items read from, or written to, one vault export.</summary>
/// <param name="Items">Vault items.</param>
internal sealed record VaultExport(IReadOnlyList<VaultItem> Items);
