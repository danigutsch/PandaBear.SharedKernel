namespace PandaBear.RepoConfig.Tool;

/// <summary>Result of publishing roadmap changes to GitHub.</summary>
/// <param name="PullRequestUrl">URL of the pull request that carries the changes.</param>
/// <param name="CommitSha">SHA of the commit that was pushed.</param>
internal sealed record RoadmapGitHubPublication(string PullRequestUrl, string CommitSha);
