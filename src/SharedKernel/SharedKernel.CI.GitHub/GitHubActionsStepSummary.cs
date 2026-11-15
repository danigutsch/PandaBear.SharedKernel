namespace PandaBear.CI.GitHub;

/// <summary>
/// Appends Markdown to the file that GitHub Actions names in <c>GITHUB_STEP_SUMMARY</c>, which the run page shows as the
/// job summary, as
/// <see href="https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#adding-a-job-summary">the
/// workflow commands</see> describe.
/// </summary>
public sealed class GitHubActionsStepSummary : IStepSummary
{
    private readonly string _path;

    /// <summary>Creates the writer of the summary file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_STEP_SUMMARY</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public GitHubActionsStepSummary(string path)
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
