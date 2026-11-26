using System.Text.Json.Serialization;

namespace SharedKernel.Repositories.GitHub;

/// <summary>Reads the caller's strict, camel-case GitHub Project taxonomy configuration.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(GitHubProjectTaxonomyConfiguration))]
internal sealed partial class GitHubProjectTaxonomyJsonContext : JsonSerializerContext;
