namespace PandaBear.RepoConfig.Tool;

/// <summary>Repository and number that locate a GitHub issue.</summary>
/// <param name="Repository">Repository in <c>owner/name</c> form.</param>
/// <param name="Issue">Issue number.</param>
internal sealed record GitHubIssueLocation(string Repository, int Issue);
