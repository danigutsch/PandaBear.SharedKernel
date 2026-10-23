namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>Catalog entry of one item. Every property except <see cref="Key"/> and <see cref="Info"/> may be edited.</summary>
/// <param name="Key">Origin key of the item, from <see cref="Matching.OriginKeys"/>. It identifies the item and may not change.</param>
/// <param name="Name">Item name.</param>
/// <param name="Folder">Folder path with <c>/</c> separators, or <see langword="null"/> for none.</param>
/// <param name="AdditionalFolders">Other folders of the item, kept in its <see cref="Model.AdditionalFolders"/> field.</param>
/// <param name="Favorite">Whether the item is a favorite.</param>
/// <param name="Notes">The item's notes, or <see langword="null"/> when it has none.</param>
/// <param name="Uris">Website addresses of a login, in order.</param>
/// <param name="Fields">Text custom fields that hold no secret finding, in order.</param>
/// <param name="Info">What the item holds besides these, for reading only.</param>
internal sealed record CatalogItem(
    string Key,
    string Name,
    string? Folder,
    IReadOnlyList<string> AdditionalFolders,
    bool Favorite,
    CatalogNotes? Notes,
    IReadOnlyList<CatalogUri> Uris,
    IReadOnlyList<CatalogField> Fields,
    CatalogInfo Info);
