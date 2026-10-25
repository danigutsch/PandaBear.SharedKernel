using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>Writes POSIX shell scripts that stand in for an executable and record that they ran.</summary>
internal static class FakeExecutable
{
    /// <summary>Writes an executable script at <paramref name="path"/> that creates <paramref name="marker"/> and fails.</summary>
    [UnsupportedOSPlatform("windows")]
    public static void WriteMarker(string path, string marker)
    {
        var quotedMarker = "'" + marker.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        WriteScript(path, ": > " + quotedMarker + "\nexit 1\n");
    }

    /// <summary>Writes an executable POSIX shell script at <paramref name="path"/> with <paramref name="body"/>.</summary>
    [UnsupportedOSPlatform("windows")]
    public static void WriteScript(string path, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
