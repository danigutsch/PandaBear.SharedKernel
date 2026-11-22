using System.Text.Json.Serialization;

namespace PandaBear.Testing.Snapshots;

/// <summary>Defines the fixed manifest wire format.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    MaxDepth = 4,
    WriteIndented = true)]
[JsonSerializable(typeof(ReproductionManifestPayload))]
internal sealed partial class ReproductionManifestJsonContext : JsonSerializerContext;
