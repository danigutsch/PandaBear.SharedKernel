namespace PandaBear.CI.GitHub;

/// <summary>
/// Appends Markdown to the file that GitHub Actions names in <c>GITHUB_STEP_SUMMARY</c>, which the run page shows as the
/// job summary, as
/// <see href="https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#adding-a-job-summary">the
/// workflow commands</see> describe.
/// </summary>
/// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_STEP_SUMMARY</c>.</param>
/// <remarks>The constructor throws <see cref="ArgumentNullException"/> when <paramref name="path"/> is <see langword="null"/>, and <see cref="ArgumentException"/> when it is not fully qualified.</remarks>
public sealed class GitHubActionsStepSummary(string path) : IStepSummary
{
    private readonly MarkdownSummaryFile _file = new(path);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public Task Append(string markdown, CancellationToken ct) => _file.Append(markdown, ct);
}
