namespace SharedKernel.Repositories.GitHub;

/// <summary>Result of a synchronization run.</summary>
/// <param name="Messages">Messages describing what was or would be changed.</param>
public sealed record GitHubSyncResult(IReadOnlyList<string> Messages);
