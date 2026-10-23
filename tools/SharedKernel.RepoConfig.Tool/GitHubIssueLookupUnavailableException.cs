namespace PandaBear.RepoConfig.Tool;

internal sealed class GitHubIssueLookupUnavailableException : InvalidOperationException
{
    public GitHubIssueLookupUnavailableException() : base("GitHub issue lookup is unavailable; resolving its current repository location.")
    {
    }
    public GitHubIssueLookupUnavailableException(string message) : base(message)
    {
    }

    public GitHubIssueLookupUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
