namespace PandaBear.Versioning.Tool;

internal sealed record CalculateReleaseOptions(
    string RepoRoot,
    string VersionKind,
    string? RunNumber,
    string? Sha,
    string? GitHubOutput,
    string? GitHubSummary);
