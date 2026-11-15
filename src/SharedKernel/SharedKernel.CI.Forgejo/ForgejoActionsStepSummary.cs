namespace PandaBear.CI.Forgejo;

/// <summary>
/// Appends Markdown to the file that Forgejo Actions names in <c>FORGEJO_STEP_SUMMARY</c> (a program that writes
/// <c>GITHUB_STEP_SUMMARY</c> is honored too), which Forgejo renders above the step's log, as
/// <see href="https://forgejo.org/docs/latest/user/actions/advanced-features/">the job summaries</see> describe. Forgejo
/// truncates what a step appends beyond 1 MiB.
/// </summary>
public sealed class ForgejoActionsStepSummary : IStepSummary
{
    private readonly string _path;

    /// <summary>Creates the writer of the summary file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>FORGEJO_STEP_SUMMARY</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public ForgejoActionsStepSummary(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The summary file must be a fully qualified path.", nameof(path));
        }

        _path = path;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public Task Append(string markdown, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return File.AppendAllTextAsync(_path, markdown, ct);
    }
}
