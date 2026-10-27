namespace PandaBear.RepoConfig.Tool;

/// <summary>Result of a synchronization run.</summary>
/// <param name="Messages">Messages describing what was or would be changed.</param>
internal sealed record GitHubSyncResult(IReadOnlyList<string> Messages);
