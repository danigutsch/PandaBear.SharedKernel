using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>Outcome of <see cref="CatalogApplier.Apply"/>.</summary>
/// <param name="Vault">The edited vault, or <see langword="null"/> when there are problems.</param>
/// <param name="ChangedItems">Number of items the catalog changed.</param>
/// <param name="Problems">Why the catalog cannot be applied; each names an item by key and name, never a value.</param>
internal sealed record CatalogApplyResult(VaultExport? Vault, int ChangedItems, IReadOnlyList<string> Problems)
{
    /// <summary>
    /// What the catalog changes, one line per change: an item line starts with the item's key and its name as read, and a
    /// vault line starts with <c>folder</c>. A line names items, fields, folders, and unmasked website addresses, never a
    /// value. Empty when there are problems.
    /// </summary>
    public IReadOnlyList<string> Changes { get; init; } = [];

    /// <summary>
    /// The lines of <see cref="Changes"/> that remove what apply keeps unless removals are allowed: unmasked website
    /// addresses, the old values of shown text fields, shared notes, and declared folders.
    /// </summary>
    public IReadOnlyList<string> Removals { get; init; } = [];
}
