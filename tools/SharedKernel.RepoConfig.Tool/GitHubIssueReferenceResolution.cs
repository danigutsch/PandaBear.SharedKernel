namespace PandaBear.RepoConfig.Tool;

internal sealed record GitHubIssueReferenceResolution(
    string Repository,
    int Number,
    string? NodeId,
    string? PreviousNodeId,
    string? Diagnostic)
{
    public bool IsResolved => Diagnostic is null;
}
