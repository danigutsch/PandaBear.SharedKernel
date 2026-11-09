using System.Text.Json.Serialization;

namespace PandaBear.Sandbox.Tool;

/// <summary>
/// Source-generated metadata for <c>.config/sharedkernel-sandbox.json</c>. Property names are camelCase and match
/// exactly. A property that the format does not define, a property given twice in one object, a missing required
/// property, a comment, and a trailing comma fail reading.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SandboxConfiguration))]
[JsonSerializable(typeof(SettingsConfiguration))]
internal sealed partial class SandboxConfigurationJsonContext : JsonSerializerContext;
