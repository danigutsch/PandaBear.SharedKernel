using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>
/// Source-generated JSON metadata for the decisions file. Enum values are written by name, and absent optional values,
/// such as the origin key of an empty choice, are left out. A property that the format does not define, or a property
/// given twice in one object, fails reading. A misspelled section or name is then reported instead of being skipped with
/// the decisions it holds, and a repeated section, such as a second <c>findings</c>, cannot replace the first one.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(DecisionsDocument))]
internal sealed partial class DecisionsJsonContext : JsonSerializerContext;
