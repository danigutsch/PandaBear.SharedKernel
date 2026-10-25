using System.Globalization;

namespace PandaBear.K6.Tool.Tests;

/// <summary>A POSIX shell script that records each argument on a line, its environment, and its process ID.</summary>
internal sealed class FakeExecutable
{
    public FakeExecutable(string directory, string name, int exitCode, string body)
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
            printf '%s' "$$" > {Quote(ProcessIdPath)}
            for argument in "$@"; do printf '%s\n' "$argument"; done > {Quote(ArgumentsPath)}
            /usr/bin/env > {Quote(EnvironmentPath)}
            {body}
            exit {exitCode.ToString(CultureInfo.InvariantCulture)}
            """);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public string Path
    {
        get;
    }

    public string ArgumentsPath
    {
        get;
    }

    public string EnvironmentPath
    {
        get;
    }

    public string ProcessIdPath
    {
        get;
    }

    public bool Started => File.Exists(ArgumentsPath);

    public string[] Arguments() => File.ReadAllLines(ArgumentsPath);

    public string[] Environment() => File.ReadAllLines(EnvironmentPath);

    public int ProcessId() => int.Parse(File.ReadAllText(ProcessIdPath), CultureInfo.InvariantCulture);

    public static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
