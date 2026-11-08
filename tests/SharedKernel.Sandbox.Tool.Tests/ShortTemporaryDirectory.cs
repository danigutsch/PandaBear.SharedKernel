namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>A directory under the temporary folder with a short name, because a socket path must stay under about 100 bytes; deleted on disposal.</summary>
internal sealed class ShortTemporaryDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public ShortTemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "skt" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the absolute path.</summary>
    public string Path
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Directory.Delete(Path, recursive: true);
    }
}
