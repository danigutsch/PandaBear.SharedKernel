namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Temporary directory with an sk-codefix- prefix that is deleted on disposal.</summary>
internal sealed class TemporaryCodeFixDirectory : IDisposable
{
    private TemporaryCodeFixDirectory(string path)
    {
        Path = path;
    }

    /// <summary>Gets the full path of the temporary directory.</summary>
    public string Path
    {
        get;
    }

    /// <summary>Creates a new uniquely named temporary directory and returns a wrapper owning it.</summary>
    public static TemporaryCodeFixDirectory Create()
    {
        return new TemporaryCodeFixDirectory(Directory.CreateTempSubdirectory("sk-codefix-").FullName);
    }

    /// <summary>Deletes the directory and its contents, ignoring IO and access errors so cleanup cannot mask a test failure.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup is best-effort so teardown does not hide assertion failures.
        }
        catch (UnauthorizedAccessException)
        {
            // Test cleanup is best-effort so teardown does not hide assertion failures.
        }
    }
}
