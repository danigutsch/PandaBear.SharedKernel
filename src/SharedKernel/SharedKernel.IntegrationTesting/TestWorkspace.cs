namespace PandaBear.IntegrationTesting;

/// <summary>
/// A private temporary folder for the files of one test run (databases, uploads, imported configuration), removed when disposed.
/// </summary>
public sealed class TestWorkspace : IAsyncDisposable
{
    private const int DeleteAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    private Action? _releaseFiles;

    private TestWorkspace(string root, Action? releaseFiles)
    {
        Root = root;
        _releaseFiles = releaseFiles;
    }

    /// <summary>Gets the full path of the folder.</summary>
    public string Root
    {
        get;
    }

    /// <summary>Creates a new empty folder under the system's temporary folder.</summary>
    /// <param name="name">A readable prefix of the folder name.</param>
    /// <returns>The workspace.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static TestWorkspace Create(string name) => Create(name, releaseFiles: null);

    /// <summary>Creates a new empty folder under the system's temporary folder, with a callback that releases its files before removal.</summary>
    /// <param name="name">A readable prefix of the folder name.</param>
    /// <param name="releaseFiles">
    /// Runs once on disposal, before the folder is deleted, to close what this process still holds open in it. For SQLite databases in the
    /// folder pass <c>SqliteConnection.ClearAllPools</c>: pooled connections keep the files open, and Windows refuses to delete an open file.
    /// <see langword="null"/> means nothing to release.
    /// </param>
    /// <returns>The workspace.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static TestWorkspace Create(string name, Action? releaseFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TestWorkspace(Directory.CreateTempSubdirectory(name + "-").FullName, releaseFiles);
    }

    /// <summary>Creates a folder inside the workspace, with its parents.</summary>
    /// <param name="relativePath">The path below the root.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is blank or leaves the workspace.</exception>
    public string Folder(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The path must stay inside the workspace.", nameof(relativePath));
        }

        return Directory.CreateDirectory(full).FullName;
    }

    /// <summary>
    /// Runs the release callback once, then removes the folder and everything in it, retrying while a stopped process still holds a file.
    /// </summary>
    /// <returns>A task that completes when the folder is gone.</returns>
    /// <exception cref="IOException">A file stayed locked after several attempts.</exception>
    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _releaseFiles, null)?.Invoke();
        for (var attempt = 1; Directory.Exists(Root); attempt++)
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < DeleteAttempts)
            {
                await Task.Delay(RetryDelay).ConfigureAwait(false);
            }
        }
    }
}
