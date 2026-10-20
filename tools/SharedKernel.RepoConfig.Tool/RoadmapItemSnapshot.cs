namespace SharedKernel.RepoConfig.Tool;

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
