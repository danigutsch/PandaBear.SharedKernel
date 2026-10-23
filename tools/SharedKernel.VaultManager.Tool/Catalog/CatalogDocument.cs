namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// On-disk shape of a catalog: the parts of a vault that people and agents may read and edit, without secret values.
/// </summary>
/// <param name="Version">Format version; <see cref="CatalogFile.CurrentVersion"/>.</param>
/// <param name="Source">The vault file the catalog was made from.</param>
/// <param name="Folders">Folders the vault declares, including folders that hold no item.</param>
/// <param name="Items">One entry per item, in the order the vault holds them.</param>
internal sealed record CatalogDocument(int Version, CatalogSource Source, IReadOnlyList<string> Folders, IReadOnlyList<CatalogItem> Items);
