using PandaBear.VaultManager.Tool.Merge;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>A catalog made by <see cref="CatalogBuilder"/>.</summary>
/// <param name="Catalog">The catalog.</param>
/// <param name="NotesUpdates">
/// Entries for the decisions file: a pending entry for each item whose notes have no decision or changed since they were
/// shared, and a share decision bound to the notes it shares for each one that named no notes yet.
/// </param>
/// <param name="Counts">What the catalog shows and withholds.</param>
internal sealed record CatalogBuild(CatalogDocument Catalog, IReadOnlyList<ItemNotesDecision> NotesUpdates, CatalogCounts Counts);
