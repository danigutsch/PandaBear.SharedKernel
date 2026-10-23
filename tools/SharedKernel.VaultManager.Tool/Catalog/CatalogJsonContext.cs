using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Catalog;

/// <summary>
/// Source-generated JSON metadata for the catalog file. Enum values are written by name. Every property is written, a
/// <see langword="null"/> one as <c>null</c>, so an editor sees each property it may set; only the optional parts of a
/// website address are left out when absent. A property that the format does not define, a property given twice in one
/// object, or a missing required property fails reading, so a misspelled edit is reported instead of being skipped.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(CatalogDocument))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
