namespace PandaBear.RepoConfig.Tool;

internal sealed record RoadmapItemSnapshot(
    string Id,
    string Path,
    string Title,
    string Type,
    string Status,
    string Theme,
    bool IsTriaged,
    int? Order,
    string? Parent,
    decimal? Reach,
    decimal? Impact,
    decimal? Confidence,
    decimal? Effort,
    IReadOnlyList<string> BlockedBy,
    IReadOnlyList<string> Blocks,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Labels,
    int? GitHubIssue,
    bool CreateGitHubIssue)
{
    public string? GitHubRepository
    {
        get; init;
    }

    public string? GitHubNodeId
    {
        get; init;
    }

    public IReadOnlyList<GitHubIssueLocation> PreviousGitHubLocations { get; init; } = [];

    public IReadOnlyList<string> PreviousGitHubNodeIds { get; init; } = [];

    public bool GitHubReferenceReadOnly
    {
        get; init;
    }

    public bool GitHubReferenceUnresolved
    {
        get; init;
    }

    public bool HasLocalGitHubIssue(string? repository) => !GitHubReferenceUnresolved
        && (GitHubIssue is not null || CreateGitHubIssue)
        && (GitHubRepository is null || string.Equals(GitHubRepository, repository, StringComparison.OrdinalIgnoreCase));

    public decimal? Score =>
        IsTriaged
        && Reach is decimal reach
        && Impact is decimal impact
        && Confidence is decimal confidence
        && Effort is decimal effort
        && effort > 0
            ? reach * impact * confidence / effort
            : null;
}
