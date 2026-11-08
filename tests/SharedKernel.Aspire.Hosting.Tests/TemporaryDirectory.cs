namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>A unique directory path under the system temp folder; the directory is deleted when disposed.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>Gets the absolute path of the directory, which is not created up front.</summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pandabear-sqlite-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
