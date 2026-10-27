namespace PandaBear.RepoConfig.Tool;

/// <summary>Identity of a GitHub sub-issue.</summary>
/// <param name="Id">Numeric issue ID.</param>
/// <param name="Url">Issue URL.</param>
internal sealed record GitHubSubissueIdentity(long Id, string Url);
