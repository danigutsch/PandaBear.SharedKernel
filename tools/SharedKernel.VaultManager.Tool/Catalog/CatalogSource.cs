namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>The vault file a catalog was made from. <c>apply</c> accepts the catalog only for this exact file.</summary>
/// <param name="File">File name of the vault, which every item key names.</param>
/// <param name="Sha256">SHA-256 of the file's bytes, in lowercase hexadecimal.</param>
internal sealed record CatalogSource(string File, string Sha256);
