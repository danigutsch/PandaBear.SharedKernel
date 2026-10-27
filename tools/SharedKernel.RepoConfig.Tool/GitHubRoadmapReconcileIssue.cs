namespace PandaBear.RepoConfig.Tool;

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
    public string? NodeId
    {
        get; init;
    }

    public string? StateReason
    {
        get; init;
    }
}
