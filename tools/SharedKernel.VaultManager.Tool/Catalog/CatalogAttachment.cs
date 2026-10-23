namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>Attachment of a catalog item.</summary>
/// <param name="Name">File name.</param>
/// <param name="Size">Size in bytes, when the export gives it.</param>
internal sealed record CatalogAttachment(string Name, long? Size);
