using System.Globalization;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>A fake <c>docker</c> script that logs every call, answers context queries, and sleeps while running or removing a container.</summary>
internal sealed class FakeDockerEngine : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Writes the executable <c>docker</c> script, whose container removal takes <c>removalDelaySeconds</c>.</summary>
    public FakeDockerEngine(int removalDelaySeconds)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The fake Docker engine requires a POSIX shell.");
        }

        LogPath = Path.Combine(_directory.Path, "docker.log");
        var scriptPath = Path.Combine(_directory.Path, "docker");
        File.WriteAllText(
            scriptPath,
            $$"""
            #!/bin/sh
            PATH=/usr/bin:/bin
            log='{{LogPath.Replace("'", "'\\''", StringComparison.Ordinal)}}'
            printf '%s\n' "$*" >> "$log"
            case " $* " in
              *" context show "*) printf 'default\n' ;;
              *" context inspect "*) printf 'unix:///var/run/docker.sock\n' ;;
              *" run "*) exec sleep 300 ;;
              *" rm --force "*) sleep {{removalDelaySeconds.ToString(CultureInfo.InvariantCulture)}}; printf 'removed\n' >> "$log" ;;
              *) exit 64 ;;
            esac
            """);
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>The directory that holds the <c>docker</c> script and so can serve as <c>PATH</c>.</summary>
    public string DirectoryPath => _directory.Path;

    /// <summary>The file the script appends each call's arguments to.</summary>
    public string LogPath
    {
        get;
    }

    /// <summary>Reads the logged calls, or an empty string when nothing was logged.</summary>
    public string ReadLog()
    {
        return File.Exists(LogPath) ? File.ReadAllText(LogPath) : string.Empty;
    }

    /// <summary>Waits until the log shows a <c>run --rm</c> call.</summary>
    public async Task WaitUntilRuntimeStarts(CancellationToken ct)
    {
        while (!ReadLog().Contains(" run --rm ", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
    }

    /// <summary>Deletes the temporary directory.</summary>
    public void Dispose()
    {
        _directory.Dispose();
    }
}
