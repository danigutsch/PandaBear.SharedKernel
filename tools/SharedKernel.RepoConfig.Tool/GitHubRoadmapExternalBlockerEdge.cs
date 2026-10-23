namespace PandaBear.RepoConfig.Tool;

internal sealed record GitHubRoadmapExternalBlockerEdge(
    GitHubRoadmapReconcileRelation Blocker,
    GitHubRoadmapReconcileRelation Blocked);
