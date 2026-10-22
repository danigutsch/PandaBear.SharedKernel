using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Source-generated JSON metadata for the decisions file.</summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(DecisionsDocument))]
internal sealed partial class DecisionsJsonContext : JsonSerializerContext;
