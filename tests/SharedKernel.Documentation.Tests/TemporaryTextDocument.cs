namespace SharedKernel.Documentation.Tests;

internal sealed class TemporaryTextDocument : IDisposable
{
    public TemporaryTextDocument(string content)
    {
        Path = System.IO.Path.GetTempFileName();
        File.WriteAllText(Path, content);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
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
