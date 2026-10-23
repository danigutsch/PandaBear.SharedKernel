using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// Source-generated JSON metadata for the brief file. Enum values are written by name. Every property is written, a
/// <see langword="null"/> one as <c>null</c>, except the optional parts of a holder, which are left out when absent. A
/// property that the format does not define, a property given twice in one object, or a missing required property fails
/// reading.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(BriefDocument))]
internal sealed partial class BriefJsonContext : JsonSerializerContext;
