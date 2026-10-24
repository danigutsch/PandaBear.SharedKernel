using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated metadata for the symbol evidence file. Reading rejects unknown, repeated, and missing properties,
/// and a path named twice.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SymbolEvidenceFile))]
internal sealed partial class SymbolEvidenceJsonContext : JsonSerializerContext;
