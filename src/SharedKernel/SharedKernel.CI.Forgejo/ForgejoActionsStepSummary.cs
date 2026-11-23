namespace PandaBear.CI.Forgejo;

/// <summary>
/// Appends Markdown to the file that Forgejo Actions names in <c>FORGEJO_STEP_SUMMARY</c> (a program that writes
/// <c>GITHUB_STEP_SUMMARY</c> is honored too), which Forgejo renders above the step's log, as
/// <see href="https://forgejo.org/docs/latest/user/actions/advanced-features/">the job summaries</see> describe. Forgejo
/// truncates what a step appends beyond 1 MiB.
/// </summary>
/// <param name="path">The absolute path of the file, such as the value of <c>FORGEJO_STEP_SUMMARY</c>.</param>
/// <remarks>The constructor throws <see cref="ArgumentNullException"/> when <paramref name="path"/> is <see langword="null"/>, and <see cref="ArgumentException"/> when it is not fully qualified.</remarks>
public sealed class ForgejoActionsStepSummary(string path) : IStepSummary
{
    private readonly MarkdownSummaryFile _file = new(path);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public Task Append(string markdown, CancellationToken ct) => _file.Append(markdown, ct);
}
