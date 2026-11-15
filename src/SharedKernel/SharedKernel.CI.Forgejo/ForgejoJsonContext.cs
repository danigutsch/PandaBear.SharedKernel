using System.Text.Json.Serialization;

namespace PandaBear.CI.Forgejo;

/// <summary>
/// Source-generated JSON metadata for the Forgejo releases API. The properties that the provider reads are required and
/// may not be null; the API's other properties are ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(ForgejoRelease[]))]
[JsonSerializable(typeof(ForgejoTaggedRelease))]
internal sealed partial class ForgejoJsonContext : JsonSerializerContext;
