using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated JSON metadata for a page of the GitHub releases API. The properties that the tool reads are
/// required and may not be null; the API's other properties are ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(GitHubRelease[]))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext;
