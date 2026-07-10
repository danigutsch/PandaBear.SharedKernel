namespace SharedKernel.Versioning;

/// <summary>
/// Detects Conventional Commit breaking-change markers in commit-message batches.
/// </summary>
public static class BreakingChangeMarker
{
    /// <summary>
    /// Determines whether any commit message contains a breaking-change marker.
    /// </summary>
    /// <param name="messages">Null-separated commit messages.</param>
    /// <returns><see langword="true" /> when a breaking-change marker exists.</returns>
    public static bool HasMarker(string messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var commits = messages.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return commits.Any(static message => HasRawBreakingFooter(message)
            || (ConventionalCommitParser.TryParse(message, out var commit) && commit is not null && commit.Impact == ReleaseImpact.Major));
    }

    private static bool HasRawBreakingFooter(string message) =>
        message.Contains("\nBREAKING CHANGE:", StringComparison.Ordinal)
        || message.Contains("\nBREAKING-CHANGE:", StringComparison.Ordinal);
}
