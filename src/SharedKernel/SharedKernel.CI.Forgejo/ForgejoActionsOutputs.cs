namespace PandaBear.CI.Forgejo;

/// <summary>
/// Writes run outputs to the file that Forgejo Actions names in <c>FORGEJO_OUTPUT</c> (also <c>GITHUB_OUTPUT</c>), one <c>name=value</c> line each, as
/// <see href="https://forgejo.org/docs/latest/user/actions/reference/">the
/// Actions reference</see> describes for step and job outputs.
/// </summary>
public sealed class ForgejoActionsOutputs : IRunOutputs
{
    private readonly string _path;

    /// <summary>Creates the writer of the output file at <paramref name="path"/>.</summary>
    /// <param name="path">The absolute path of the file, such as the value of <c>FORGEJO_OUTPUT</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not fully qualified.</exception>
    public ForgejoActionsOutputs(string path)
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
