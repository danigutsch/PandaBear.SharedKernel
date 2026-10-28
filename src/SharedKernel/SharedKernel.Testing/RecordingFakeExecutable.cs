using System.Globalization;

namespace PandaBear.Testing;

/// <summary>A POSIX shell script that records each argument on a line, its environment, and its process ID.</summary>
public sealed class RecordingFakeExecutable
{
    /// <summary>Writes the recording shell script into <c>directory</c> and, on Unix, marks it executable.</summary>
    public RecordingFakeExecutable(string directory, string name, int exitCode, string body)
    {
        Directory.CreateDirectory(directory);
        Path = System.IO.Path.Combine(directory, name);
        ArgumentsPath = Path + ".args";
        EnvironmentPath = Path + ".env";
        ProcessIdPath = Path + ".pid";
        File.WriteAllText(
            Path,
            $"""
            #!/bin/sh
            printf '%s' "$$" > {FakeExecutable.Quote(ProcessIdPath)}
            for argument in "$@"; do printf '%s\n' "$argument"; done > {FakeExecutable.Quote(ArgumentsPath)}
            /usr/bin/env > {FakeExecutable.Quote(EnvironmentPath)}
            {body}
            exit {exitCode.ToString(CultureInfo.InvariantCulture)}
            """);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
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

    /// <summary>Whether the script has run, which is when it has written its arguments file.</summary>
    public bool Started => File.Exists(ArgumentsPath);

    /// <summary>Reads the arguments the script was called with, one entry per line.</summary>
    public string[] Arguments() => File.ReadAllLines(ArgumentsPath);

    /// <summary>Reads the environment the script ran with, one <c>NAME=value</c> entry per line.</summary>
    public string[] Environment() => File.ReadAllLines(EnvironmentPath);

    /// <summary>Reads the process ID the script recorded.</summary>
    public int ProcessId() => int.Parse(File.ReadAllText(ProcessIdPath), CultureInfo.InvariantCulture);
}
