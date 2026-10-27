namespace PandaBear.Documentation.Tests;

/// <summary>Temporary text file that is deleted on disposal, ignoring cleanup failures.</summary>
internal sealed class TemporaryTextDocument : IDisposable
{
    /// <summary>Creates a temporary file and writes the content into it.</summary>
    /// <param name="content">The text to write.</param>
    public TemporaryTextDocument(string content)
    {
        Path = System.IO.Path.GetTempFileName();
        File.WriteAllText(Path, content);
    }

    /// <summary>Gets the path of the temporary file.</summary>
    public string Path
    {
        get;
    }

    /// <inheritdoc />
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
