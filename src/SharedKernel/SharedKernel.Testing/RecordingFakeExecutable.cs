using System.Globalization;

namespace PandaBear.Testing;

/// <summary>A POSIX shell script that records each argument on a line, its environment, and its process ID.</summary>
public sealed class RecordingFakeExecutable
{
    /// <summary>Writes the recording shell script into <c>directory</c> and marks it executable. Throws <see cref="PlatformNotSupportedException" /> on Windows.</summary>
    /// <param name="directory">The directory that receives the script; it is created when missing.</param>
    /// <param name="name">The script file name.</param>
    /// <param name="exitCode">The exit code the script returns.</param>
    /// <param name="body">Shell commands run after the script has recorded its process ID, environment and arguments.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> or <paramref name="name"/> or <paramref name="body"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is Windows.</exception>
    public RecordingFakeExecutable(string directory, string name, int exitCode, string body)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(body);

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Recording fake executables are POSIX shell scripts.");
        }

        Path = System.IO.Path.Combine(directory, name);
        ArgumentsPath = Path + ".args";
        EnvironmentPath = Path + ".env";
        ProcessIdPath = Path + ".pid";
        FakeExecutable.WriteScript(
            Path,
            $"""
            printf '%s' "$$" > {FakeExecutable.Quote(ProcessIdPath)}
            /usr/bin/env > {FakeExecutable.Quote(EnvironmentPath)}
            for argument in "$@"; do printf '%s\n' "$argument"; done > {FakeExecutable.Quote(ArgumentsPath)}
            {body}
            exit {exitCode.ToString(CultureInfo.InvariantCulture)}
            """);
    }

    /// <summary>The absolute path of the script.</summary>
    public string Path
    {
        get;
    }

    /// <summary>The file the script writes its arguments to, one per line.</summary>
    public string ArgumentsPath
    {
        get;
    }

    /// <summary>The file the script writes its environment to.</summary>
    public string EnvironmentPath
    {
        get;
    }

    /// <summary>The file the script writes its process ID to.</summary>
    public string ProcessIdPath
    {
        get;
    }

    /// <summary>Whether the script has recorded its process ID, environment and arguments; the arguments file is written last, before <c>body</c> runs.</summary>
    public bool Started => File.Exists(ArgumentsPath);

    /// <summary>Reads the arguments the script was called with, one entry per line.</summary>
    public string[] Arguments() => File.ReadAllLines(ArgumentsPath);

    /// <summary>Reads the environment the script ran with, one <c>NAME=value</c> entry per line.</summary>
    public string[] Environment() => File.ReadAllLines(EnvironmentPath);

    /// <summary>Reads the process ID the script recorded.</summary>
    public int ProcessId() => int.Parse(File.ReadAllText(ProcessIdPath), CultureInfo.InvariantCulture);
}
