using System.Text.Json.Serialization;

namespace PandaBear.IO.Tests;

/// <summary>The inventory fixture’s strict serialization contract.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ArtifactInventory))]
internal sealed partial class ArtifactManifestJsonContext : JsonSerializerContext;
