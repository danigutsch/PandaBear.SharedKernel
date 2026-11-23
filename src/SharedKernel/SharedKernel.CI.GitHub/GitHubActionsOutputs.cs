namespace PandaBear.CI.GitHub;

/// <summary>
/// Writes run outputs to the file that GitHub Actions names in <c>GITHUB_OUTPUT</c>, one <c>name=value</c> line each, as
/// <see href="https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#setting-an-output-parameter">the
/// workflow commands</see> describe.
/// </summary>
/// <param name="path">The absolute path of the file, such as the value of <c>GITHUB_OUTPUT</c>.</param>
/// <remarks>The constructor throws <see cref="ArgumentNullException"/> when <paramref name="path"/> is <see langword="null"/>, and <see cref="ArgumentException"/> when it is not fully qualified.</remarks>
public sealed class GitHubActionsOutputs(string path) : IRunOutputs
{
    private readonly KeyValueOutputFile _file = new(path);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public Task Write(IReadOnlyList<KeyValuePair<string, string>> outputs, CancellationToken ct) => _file.Write(outputs, ct);
}
