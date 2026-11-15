namespace PandaBear.Vault.Catalog;

/// <summary>Attachment of a catalog item.</summary>
/// <param name="Name">File name.</param>
/// <param name="Size">Size in bytes, when the export gives it.</param>
public sealed record CatalogAttachment(string Name, long? Size);
