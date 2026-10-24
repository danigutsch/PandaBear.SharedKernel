using System.Text.Json.Serialization;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Source-generated metadata for <c>.config/sharedkernel-repo.json</c>. Property names are camelCase and match exactly.
/// A property that the format does not define, a property given twice in one object, a missing required property, a
/// <see langword="null"/> where a value is required, a comment, and a trailing comma fail reading.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ConsumerConfiguration))]
internal sealed partial class ConsumerConfigurationJsonContext : JsonSerializerContext;
