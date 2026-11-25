using System.Text.Json.Serialization;

namespace SharedKernel.LoadTesting;

/// <summary>
/// Source-generated metadata for the load-test configuration file. Property names are camelCase and match exactly. A
/// property that the format does not define, a property given twice in one object, a missing required property, a
/// <see langword="null"/> where a value is required, a comment, and a trailing comma fail reading.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(LoadTestConfiguration))]
internal sealed partial class LoadTestConfigurationJsonContext : JsonSerializerContext;
