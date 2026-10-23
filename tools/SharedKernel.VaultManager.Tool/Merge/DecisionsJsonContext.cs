using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Source-generated JSON metadata for the decisions file. Enum values are written by name, and absent optional values,
/// such as the origin key of an empty choice, are left out.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(DecisionsDocument))]
internal sealed partial class DecisionsJsonContext : JsonSerializerContext;
