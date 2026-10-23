using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>A catalog made by <see cref="CatalogBuilder"/>.</summary>
/// <param name="Catalog">The catalog.</param>
/// <param name="PendingNotes">Items whose notes have no decision, as pending entries for the decisions file.</param>
/// <param name="Counts">What the catalog shows and withholds.</param>
internal sealed record CatalogBuild(CatalogDocument Catalog, IReadOnlyList<ItemNotesDecision> PendingNotes, CatalogCounts Counts);
