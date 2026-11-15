namespace PandaBear.Vault.Catalog;

/// <summary>Text custom field of a catalog item.</summary>
/// <param name="Name">Field name.</param>
/// <param name="Value">Field value.</param>
public sealed record CatalogField(string Name, string Value);
