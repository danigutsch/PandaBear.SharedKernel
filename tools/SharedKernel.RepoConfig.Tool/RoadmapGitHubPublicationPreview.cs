namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Describes the draft pull request that roadmap publication would create, without file contents.
/// </summary>
/// <param name="Repository">The repository, as <c>owner/repository</c>.</param>
/// <param name="BaseSha">The approved base commit of the new branch.</param>
/// <param name="BranchName">The branch that publication would create.</param>
/// <param name="Paths">The repository paths that the pull request would change, in ordinal order.</param>
internal sealed record RoadmapGitHubPublicationPreview(
    string Repository,
    string BaseSha,
    string BranchName,
    IReadOnlyList<string> Paths);
