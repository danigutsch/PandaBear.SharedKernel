namespace PandaBear.RepoConfig.Tool;

/// <summary>Read-only view of one roadmap item file, including its triage scores, relationships, and GitHub issue reference.</summary>
/// <param name="Id">Roadmap item id.</param>
/// <param name="Path">Repository-relative path of the item file.</param>
/// <param name="Title">Item title.</param>
/// <param name="Type">Item type from the allowed types.</param>
/// <param name="Status">Item status from the allowed statuses.</param>
/// <param name="Theme">Theme the item belongs to.</param>
/// <param name="IsTriaged">Whether the item has been triaged and its scores may be used.</param>
/// <param name="Order">Explicit priority position, or null when unordered.</param>
/// <param name="Parent">Id of the parent item, or null.</param>
/// <param name="Reach">Reach score used for prioritization, or null.</param>
/// <param name="Impact">Impact score, or null.</param>
/// <param name="Confidence">Confidence score, or null.</param>
/// <param name="Effort">Effort estimate, or null.</param>
/// <param name="BlockedBy">Ids of items that block this one.</param>
/// <param name="Blocks">Ids of items this one blocks.</param>
/// <param name="Dependencies">Ids of items this one depends on.</param>
/// <param name="Tags">Free-form tags.</param>
/// <param name="Labels">GitHub labels to apply to the linked issue.</param>
/// <param name="GitHubIssue">Linked GitHub issue number, or null.</param>
/// <param name="CreateGitHubIssue">Whether a GitHub issue should be created for the item.</param>
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
    /// <summary>Repository the linked issue lives in, or null when it is the project repository.</summary>
    public string? GitHubRepository
    {
        get; init;
    }

    /// <summary>GraphQL node id of the linked GitHub issue, or null.</summary>
    public string? GitHubNodeId
    {
        get; init;
    }

    /// <summary>Earlier locations of the linked issue, kept after the issue was transferred.</summary>
    public IReadOnlyList<GitHubIssueLocation> PreviousGitHubLocations { get; init; } = [];

    /// <summary>Earlier node ids of the linked issue, kept after the issue was transferred.</summary>
    public IReadOnlyList<string> PreviousGitHubNodeIds { get; init; } = [];

    /// <summary>Whether the item has no local issue to write to, so its GitHub reference must not be changed.</summary>
    public bool GitHubReferenceReadOnly
    {
        get; init;
    }

    /// <summary>Whether the GitHub issue reference could not be resolved.</summary>
    public bool GitHubReferenceUnresolved
    {
        get; init;
    }

    /// <summary>Returns whether the item links, or is set to create, an issue in <paramref name="repository"/> and the reference is resolved.</summary>
    public bool HasLocalGitHubIssue(string? repository) => !GitHubReferenceUnresolved
        && (GitHubIssue is not null || CreateGitHubIssue)
        && (GitHubRepository is null || string.Equals(GitHubRepository, repository, StringComparison.OrdinalIgnoreCase));

    /// <summary>RICE score (reach times impact times confidence, divided by effort) for a triaged item with positive effort, otherwise null.</summary>
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
