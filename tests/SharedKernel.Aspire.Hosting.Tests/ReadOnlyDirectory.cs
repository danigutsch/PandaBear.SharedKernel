using System.Runtime.Versioning;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Makes a directory read-only for its lifetime and restores its permissions when disposed.</summary>
[UnsupportedOSPlatform("windows")]
internal sealed class ReadOnlyDirectory : IDisposable
{
    private readonly string path;
    private readonly UnixFileMode original;

    /// <summary>Initializes a new instance of the <see cref="ReadOnlyDirectory"/> class and removes the write permission.</summary>
    /// <param name="path">The existing directory.</param>
    public ReadOnlyDirectory(string path)
    {
        this.path = path;
        original = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
    }

    /// <summary>Gets a value indicating whether the process honors directory permissions (a non-root Unix user), for <c>SkipUnless</c> conditions.</summary>
    public static bool IsEnforced => !OperatingSystem.IsWindows() && Environment.UserName != "root";

    /// <inheritdoc />
    public void Dispose() => File.SetUnixFileMode(path, original);
}
