namespace PandaBear.RepoConfig.Tool;

/// <summary>Reference to a related GitHub issue.</summary>
/// <param name="Number">Issue number.</param>
/// <param name="Repository">Repository in <c>owner/name</c> form.</param>
/// <param name="State">Issue state.</param>
internal sealed record GitHubRoadmapReconcileRelation(int Number, string Repository, string State);
