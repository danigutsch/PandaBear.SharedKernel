using System.Runtime.Versioning;

namespace PandaBear.Versioning.Tests;

/// <summary>Writes POSIX shell scripts that stand in for an executable and record that they ran.</summary>
internal static class FakeExecutable
{
    /// <summary>Writes an executable script at <paramref name="path"/> that creates <paramref name="marker"/> and fails.</summary>
    [UnsupportedOSPlatform("windows")]
    public static void WriteMarker(string path, string marker)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var quotedMarker = "'" + marker.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        File.WriteAllText(path, "#!/bin/sh\n: > " + quotedMarker + "\nexit 1\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
