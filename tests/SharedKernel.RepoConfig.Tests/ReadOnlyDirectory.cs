using System.Runtime.Versioning;

namespace PandaBear.RepoConfig.Tests;

/// <summary>A directory that cannot be written to until it is disposed.</summary>
[UnsupportedOSPlatform("windows")]
internal sealed class ReadOnlyDirectory : IDisposable
{
    private readonly string _path;

    /// <summary>Creates <paramref name="path"/> and removes its write permission.</summary>
    public ReadOnlyDirectory(string path)
    {
        _path = path;
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
    }

    /// <summary>Restores write permission so that the workspace can be deleted.</summary>
    public void Dispose() => File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
