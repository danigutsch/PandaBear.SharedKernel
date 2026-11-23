namespace PandaBear.CI.Forgejo;

/// <summary>
/// Writes run outputs to the file that Forgejo Actions names in <c>FORGEJO_OUTPUT</c> (also <c>GITHUB_OUTPUT</c>), one <c>name=value</c> line each, as
/// <see href="https://forgejo.org/docs/latest/user/actions/reference/">the
/// Actions reference</see> describes for step and job outputs.
/// </summary>
/// <param name="path">The absolute path of the file, such as the value of <c>FORGEJO_OUTPUT</c>.</param>
/// <remarks>The constructor throws <see cref="ArgumentNullException"/> when <paramref name="path"/> is <see langword="null"/>, and <see cref="ArgumentException"/> when it is not fully qualified.</remarks>
public sealed class ForgejoActionsOutputs(string path) : IRunOutputs
{
    private readonly KeyValueOutputFile _file = new(path);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public Task Write(IReadOnlyList<KeyValuePair<string, string>> outputs, CancellationToken ct) => _file.Write(outputs, ct);
}
