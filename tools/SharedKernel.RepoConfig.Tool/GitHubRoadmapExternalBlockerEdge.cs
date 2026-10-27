namespace PandaBear.RepoConfig.Tool;

/// <summary>Blocking relationship where at least one issue lives outside the roadmap repository.</summary>
/// <param name="Blocker">Blocking issue.</param>
/// <param name="Blocked">Blocked issue.</param>
internal sealed record GitHubRoadmapExternalBlockerEdge(
    GitHubRoadmapReconcileRelation Blocker,
    GitHubRoadmapReconcileRelation Blocked);
