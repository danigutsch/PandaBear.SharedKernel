namespace SharedKernel.CI;

/// <summary>
/// Verifies that a pull request title or a commit message header is a Conventional Commit header and that breaking-change
/// footers have a description.
/// </summary>
public static class CommitMessageVerifier
{
    /// <summary>Verifies the title and body of the pull request in a <c>pull_request</c> event file.</summary>
    /// <param name="eventPath">The event JSON file, whose title is the header and whose body holds the footers.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <exception cref="ArgumentException">The title or a breaking-change footer is not valid.</exception>
    /// <exception cref="IOException">The event cannot be read.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task VerifyEvent(string eventPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(eventPath);
        return CommitMessageVerification.VerifyEvent(eventPath, ct);
    }

    /// <summary>Verifies a Git commit message file, such as the one a <c>commit-msg</c> hook receives.</summary>
    /// <param name="messagePath">The message file.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <exception cref="ArgumentException">The message has no header, or the header or a breaking-change footer is not valid.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task VerifyFile(string messagePath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messagePath);
        return CommitMessageVerification.VerifyFile(messagePath, ct);
    }
}
