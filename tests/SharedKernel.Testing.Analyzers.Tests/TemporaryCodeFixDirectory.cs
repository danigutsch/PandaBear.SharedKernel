namespace SharedKernel.Testing.Analyzers.Tests;

internal sealed class TemporaryCodeFixDirectory : IDisposable
{
    private TemporaryCodeFixDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryCodeFixDirectory Create()
    {
        return new TemporaryCodeFixDirectory(Directory.CreateTempSubdirectory("sk-codefix-").FullName);
    }

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
