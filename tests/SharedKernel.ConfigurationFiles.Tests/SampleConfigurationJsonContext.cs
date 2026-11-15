using System.Text.Json.Serialization;

namespace PandaBear.ConfigurationFiles.Tests;

/// <summary>Strict source-generated metadata for <see cref="SampleConfiguration"/>, as tools declare it.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SampleConfiguration))]
internal sealed partial class SampleConfigurationJsonContext : JsonSerializerContext;
