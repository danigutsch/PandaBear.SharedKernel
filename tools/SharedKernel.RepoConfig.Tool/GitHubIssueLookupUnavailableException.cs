namespace PandaBear.RepoConfig.Tool;

/// <summary>Thrown when the current location of a GitHub issue cannot be looked up.</summary>
internal sealed class GitHubIssueLookupUnavailableException : InvalidOperationException
{
    /// <summary>Creates the exception with a default message.</summary>
    public GitHubIssueLookupUnavailableException() : base("GitHub issue lookup is unavailable; resolving its current repository location.")
    {
    }
    /// <summary>Creates the exception with the given message.</summary>
    /// <param name="message">Error message.</param>
    public GitHubIssueLookupUnavailableException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with the given message and cause.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Exception that caused this one.</param>
    public GitHubIssueLookupUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
