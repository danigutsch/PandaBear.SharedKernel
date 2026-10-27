namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>A uniquely named directory under the system temp path that is deleted with its contents on dispose.</summary>
internal sealed class TemporaryProjectDirectory : IDisposable
{
    private TemporaryProjectDirectory(string path)
    {
        Path = path;
    }

    /// <summary>Gets the full path of the temporary directory.</summary>
    public string Path
    {
        get;
    }

    /// <summary>Creates the temporary directory on disk.</summary>
    /// <returns>A wrapper that owns the newly created directory.</returns>
    public static TemporaryProjectDirectory Create()
    {
        return new TemporaryProjectDirectory(CodeFixRunnerTestProject.CreateTemporaryProject());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
