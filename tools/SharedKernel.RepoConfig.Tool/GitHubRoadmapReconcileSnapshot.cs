namespace PandaBear.RepoConfig.Tool;

/// <summary>Snapshot of GitHub issue state used for roadmap reconciliation.</summary>
/// <param name="RepositoryCommit">Repository commit at snapshot time, if known.</param>
/// <param name="Issues">Issues in the snapshot.</param>
internal sealed record GitHubRoadmapReconcileSnapshot(string? RepositoryCommit, IReadOnlyList<GitHubRoadmapReconcileIssue> Issues)
{
    /// <summary>Resolution result of each referenced issue, keyed by reference.</summary>
    public IReadOnlyDictionary<string, GitHubIssueReferenceResolution> ReferenceResolutions
    {
        get; init;
    }
        = new Dictionary<string, GitHubIssueReferenceResolution>(StringComparer.Ordinal);
}
