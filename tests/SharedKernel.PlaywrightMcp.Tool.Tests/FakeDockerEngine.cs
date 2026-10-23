using System.Globalization;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

internal sealed class FakeDockerEngine : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

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

    public string DirectoryPath => _directory.Path;

    public string LogPath
    {
        get;
    }

    public string ReadLog()
    {
        return File.Exists(LogPath) ? File.ReadAllText(LogPath) : string.Empty;
    }

    public async Task WaitUntilRuntimeStarts(CancellationToken ct)
    {
        while (!ReadLog().Contains(" run --rm ", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
