using System.Text.Json.Serialization;

namespace SharedKernel.Repositories.GitHub;

/// <summary>Source-generated JSON serialization context for the GitHub project responses.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubProjectResponse), TypeInfoPropertyName = "ProjectResponse")]
[JsonSerializable(typeof(GitHubProjectItemResponse), TypeInfoPropertyName = "ProjectItemResponse")]
internal sealed partial class GitHubProjectJsonContext : JsonSerializerContext;
