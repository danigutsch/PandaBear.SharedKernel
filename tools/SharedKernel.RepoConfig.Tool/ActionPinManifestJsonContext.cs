using System.Text.Json.Serialization;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Source-generated metadata for the action pin manifest. Property names are camelCase; an unknown or repeated
/// property, a missing property, and a <see langword="null"/> where a value is required fail reading.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ActionPinManifest))]
internal sealed partial class ActionPinManifestJsonContext : JsonSerializerContext;
