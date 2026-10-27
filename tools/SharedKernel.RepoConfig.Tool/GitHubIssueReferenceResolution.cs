namespace PandaBear.RepoConfig.Tool;

/// <summary>Result of resolving the current repository location and node ID of a referenced GitHub issue.</summary>
/// <param name="Repository">Current repository in <c>owner/name</c> form.</param>
/// <param name="Number">Issue number.</param>
/// <param name="NodeId">GraphQL node ID, or <see langword="null" /> when unresolved.</param>
/// <param name="PreviousNodeId">Node ID recorded before resolution, or <see langword="null" />.</param>
/// <param name="Diagnostic">Reason the issue could not be resolved, or <see langword="null" /> when it was.</param>
internal sealed record GitHubIssueReferenceResolution(
    string Repository,
    int Number,
    string? NodeId,
    string? PreviousNodeId,
    string? Diagnostic)
{
    /// <summary>Whether the issue was resolved without a diagnostic.</summary>
    public bool IsResolved => Diagnostic is null;
}
