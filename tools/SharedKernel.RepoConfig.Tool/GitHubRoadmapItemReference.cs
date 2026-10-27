using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Reads the GitHub issue reference of a roadmap item.</summary>
internal static class GitHubRoadmapItemReference
{
    /// <summary>Returns the item updated with the GitHub integration data found in the item JSON.</summary>
    /// <param name="item">Item to update.</param>
    /// <param name="root">Root JSON element of the item file.</param>
    public static RoadmapItemSnapshot Read(RoadmapItemSnapshot item, JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || integrations.ValueKind != JsonValueKind.Object
            || !integrations.TryGetProperty("github", out var github) || github.ValueKind != JsonValueKind.Object)
        {
            return item;
        }

        return item with
        {
            GitHubRepository = ReadString(github, "repository"),
            GitHubNodeId = ReadString(github, "nodeId"),
            PreviousGitHubLocations = ReadLocations(github),
            PreviousGitHubNodeIds = github.TryGetProperty("previousNodeIds", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).ToArray()
                : []
        };
    }

    private static GitHubIssueLocation[] ReadLocations(JsonElement github) => github.TryGetProperty("previousLocations", out var locations)
        && locations.ValueKind == JsonValueKind.Array
        ? locations.EnumerateArray().Where(location => location.ValueKind == JsonValueKind.Object
            && location.TryGetProperty("repository", out var repository) && repository.ValueKind == JsonValueKind.String
            && location.TryGetProperty("issue", out var issue) && issue.ValueKind == JsonValueKind.Number && issue.TryGetInt32(out _))
            .Select(location => new GitHubIssueLocation(location.GetProperty("repository").GetString()!, location.GetProperty("issue").GetInt32())).ToArray()
        : [];

    private static string? ReadString(JsonElement root, string name) => root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
