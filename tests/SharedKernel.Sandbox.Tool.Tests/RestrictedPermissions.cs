namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Sets the Unix permissions of a file or folder for the lifetime of the object and restores them on disposal, so cleanup can still delete it.</summary>
internal sealed class RestrictedPermissions : IDisposable
{
    private readonly string _path;
    private readonly UnixFileMode _restoreMode;

    /// <summary>Applies <paramref name="mode"/> to <paramref name="path"/>.</summary>
    /// <param name="path">The file or folder.</param>
    /// <param name="mode">The restricted mode.</param>
    /// <param name="restoreMode">The mode to put back on disposal.</param>
    public RestrictedPermissions(string path, UnixFileMode mode, UnixFileMode restoreMode)
    {
        _path = path;
        _restoreMode = restoreMode;
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_path, _restoreMode);
        }
    }
}
