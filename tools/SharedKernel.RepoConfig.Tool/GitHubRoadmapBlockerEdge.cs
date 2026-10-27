namespace PandaBear.RepoConfig.Tool;

/// <summary>Blocking relationship between two roadmap issues.</summary>
/// <param name="Blocker">Issue number of the blocking issue.</param>
/// <param name="BlockerState">State of the blocking issue.</param>
/// <param name="Blocked">Issue number of the blocked issue.</param>
/// <param name="BlockedState">State of the blocked issue.</param>
internal sealed record GitHubRoadmapBlockerEdge(int Blocker, string BlockerState, int Blocked, string BlockedState);
