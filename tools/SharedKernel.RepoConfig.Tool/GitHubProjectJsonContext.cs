using System.Text.Json.Serialization;

namespace SharedKernel.RepoConfig.Tool;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubProjectResponse), TypeInfoPropertyName = "ProjectResponse")]
[JsonSerializable(typeof(GitHubProjectItemResponse), TypeInfoPropertyName = "ProjectItemResponse")]
internal sealed partial class GitHubProjectJsonContext : JsonSerializerContext;
