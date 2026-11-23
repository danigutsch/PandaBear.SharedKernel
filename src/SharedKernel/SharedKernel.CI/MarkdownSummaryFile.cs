namespace PandaBear.CI;

/// <summary>
/// Appends Markdown to a step summary file, such as the <c>GITHUB_STEP_SUMMARY</c> file of GitHub Actions or the
/// <c>FORGEJO_STEP_SUMMARY</c> file of Forgejo Actions, as UTF-8 without a byte order mark.
/// </summary>
public sealed class MarkdownSummaryFile : IStepSummary
{
    private readonly string _path;

    /// <summary>Creates the writer of the summary file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_STEP_SUMMARY</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public MarkdownSummaryFile(string path)
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
