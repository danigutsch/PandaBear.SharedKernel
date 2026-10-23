using System.Text.Json.Serialization;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Website address of a catalog item. An address with user info, a query, or a fragment, which can hold credentials or
/// tokens, is masked: it gives <see cref="Masked"/> and <see cref="Index"/>, may be moved, and may not be changed or removed.
/// Any other address gives <see cref="Value"/> and may be edited.
/// </summary>
/// <param name="Value">The address, for an address that is not masked.</param>
/// <param name="Match">Autofill match detection, or <see langword="null"/> for the vault default.</param>
/// <param name="Masked">The masked address, for an address that is masked.</param>
/// <param name="Index">Position of a masked address in the item as read.</param>
internal sealed record CatalogUri(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UriMatch? Match = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Masked = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Index = null);
