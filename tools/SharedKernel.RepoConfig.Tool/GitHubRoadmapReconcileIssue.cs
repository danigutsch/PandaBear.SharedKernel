namespace SharedKernel.RepoConfig.Tool;

internal sealed record GitHubRoadmapReconcileIssue(
    int Number,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    GitHubRoadmapReconcileRelation? Parent,
    IReadOnlyList<GitHubRoadmapReconcileRelation> SubIssues,
    IReadOnlyList<GitHubRoadmapReconcileRelation> BlockedBy,
    IReadOnlyList<GitHubRoadmapReconcileRelation> Blocking);
