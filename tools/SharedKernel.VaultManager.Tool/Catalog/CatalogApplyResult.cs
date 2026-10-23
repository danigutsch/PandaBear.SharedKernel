using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>Outcome of <see cref="CatalogApplier.Apply"/>.</summary>
/// <param name="Vault">The edited vault, or <see langword="null"/> when there are problems.</param>
/// <param name="ChangedItems">Number of items the catalog changed.</param>
/// <param name="Problems">Why the catalog cannot be applied; each names an item by key and name, never a value.</param>
internal sealed record CatalogApplyResult(VaultExport? Vault, int ChangedItems, IReadOnlyList<string> Problems);
