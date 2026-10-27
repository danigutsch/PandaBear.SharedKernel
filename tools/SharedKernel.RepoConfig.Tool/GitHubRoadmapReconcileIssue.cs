namespace PandaBear.RepoConfig.Tool;

/// <summary>Roadmap-relevant state of one GitHub issue.</summary>
/// <param name="Number">Issue number.</param>
/// <param name="Title">Issue title.</param>
/// <param name="State">Issue state.</param>
/// <param name="Labels">Label names.</param>
/// <param name="Parent">Parent issue, if any.</param>
/// <param name="SubIssues">Sub-issues.</param>
/// <param name="BlockedBy">Issues that block this one.</param>
/// <param name="Blocking">Issues this one blocks.</param>
internal sealed record GitHubRoadmapReconcileIssue(
    int Number,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    GitHubRoadmapReconcileRelation? Parent,
    IReadOnlyList<GitHubRoadmapReconcileRelation> SubIssues,
    IReadOnlyList<GitHubRoadmapReconcileRelation> BlockedBy,
    IReadOnlyList<GitHubRoadmapReconcileRelation> Blocking)
{
    /// <summary>GraphQL node ID of the issue, if known.</summary>
    public string? NodeId
    {
        get; init;
    }

    /// <summary>Reason the issue was closed, if any.</summary>
    public string? StateReason
    {
        get; init;
    }
}
