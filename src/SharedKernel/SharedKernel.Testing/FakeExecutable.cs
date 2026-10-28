using System.Runtime.Versioning;

namespace PandaBear.Testing;

/// <summary>Writes executable POSIX shell scripts that stand in for external tools.</summary>
public static class FakeExecutable
{
    /// <summary>Writes an executable script that creates an empty marker file and exits with code 1.</summary>
    /// <param name="path">The script path; missing parent directories are created.</param>
    /// <param name="marker">The path of the marker file the script creates when it runs.</param>
    [UnsupportedOSPlatform("windows")]
    public static void WriteMarker(string path, string marker)
    {
        ArgumentNullException.ThrowIfNull(marker);

        WriteScript(path, ": > " + Quote(marker) + "\nexit 1\n");
    }

    /// <summary>Writes an executable <c>/bin/sh</c> script.</summary>
    /// <param name="path">The script path; missing parent directories are created.</param>
    /// <param name="body">The script body placed after the shebang line.</param>
    [UnsupportedOSPlatform("windows")]
    public static void WriteScript(string path, string body)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(body);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Quotes a value for a POSIX shell single-quoted string.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
