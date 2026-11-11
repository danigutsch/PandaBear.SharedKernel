namespace PandaBear.IntegrationTesting;

/// <summary>
/// A private temporary folder for the files of one test run (databases, uploads, imported configuration), removed when disposed.
/// </summary>
public sealed class TestWorkspace : IAsyncDisposable
{
    private const int DeleteAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    private TestWorkspace(string root) => Root = root;

    /// <summary>Gets the full path of the folder.</summary>
    public string Root
    {
        get;
    }

    /// <summary>Creates a new empty folder under the system's temporary folder.</summary>
    /// <param name="name">A readable prefix of the folder name.</param>
    /// <returns>The workspace.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static TestWorkspace Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TestWorkspace(Directory.CreateTempSubdirectory(name + "-").FullName);
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

    /// <summary>Removes the folder and everything in it, retrying while a stopped process still holds a file.</summary>
    /// <returns>A task that completes when the folder is gone.</returns>
    /// <exception cref="IOException">A file stayed locked after several attempts.</exception>
    public async ValueTask DisposeAsync()
    {
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
