namespace PandaBear.RepoConfig.Tool;

internal sealed record GitHubRoadmapReconcileSnapshot(string? RepositoryCommit, IReadOnlyList<GitHubRoadmapReconcileIssue> Issues)
{
    public IReadOnlyDictionary<string, GitHubIssueReferenceResolution> ReferenceResolutions
    {
        get; init;
    }
        = new Dictionary<string, GitHubIssueReferenceResolution>(StringComparer.Ordinal);
}
