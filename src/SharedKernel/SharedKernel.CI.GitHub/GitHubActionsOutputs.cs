namespace PandaBear.CI.GitHub;

/// <summary>
/// Writes run outputs to the file that GitHub Actions names in <c>GITHUB_OUTPUT</c>, one <c>name=value</c> line each, as
/// <see href="https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#setting-an-output-parameter">the
/// workflow commands</see> describe.
/// </summary>
public sealed class GitHubActionsOutputs : IRunOutputs
{
    private readonly string _path;

    /// <summary>Creates the writer of the output file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_OUTPUT</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public GitHubActionsOutputs(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The output file must be a fully qualified path.", nameof(path));
        }

        _path = path;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public Task Write(IReadOnlyList<KeyValuePair<string, string>> outputs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return File.AppendAllTextAsync(_path, OutputLines.Format(outputs), ct);
    }
}
