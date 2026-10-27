using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>A long-running shell or PowerShell child that records its process ID and start time so tests can tell whether that exact process is still alive.</summary>
internal sealed class CancellableChildProcess : IAsyncDisposable
{
    private readonly string _pidFile = Path.Combine(
        Path.GetTempPath(),
        $"sharedkernel-playwright-mcp-{Guid.NewGuid():N}.pid");
    private int? _pid;
    private DateTime? _startTimeUtc;

    /// <summary>Prepares the start info of a child that writes its PID to a temporary file and then sleeps for five minutes.</summary>
    public CancellableChildProcess()
    {
        StartInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (OperatingSystem.IsWindows())
        {
            StartInfo.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            StartInfo.ArgumentList.Add("-NoProfile");
            StartInfo.ArgumentList.Add("-NonInteractive");
            StartInfo.ArgumentList.Add("-Command");
            StartInfo.ArgumentList.Add(
                "[System.IO.File]::WriteAllText($env:PID_FILE, [string]$PID); [System.Threading.Thread]::Sleep(300000)");
        }
        else
        {
            StartInfo.FileName = "/bin/sh";
            StartInfo.ArgumentList.Add("-c");
            StartInfo.ArgumentList.Add("printf %s \"$$\" > \"$PID_FILE\"; exec sleep 300");
        }

        StartInfo.Environment["PID_FILE"] = _pidFile;
    }

    /// <summary>The start info to launch the child with.</summary>
    public ProcessStartInfo StartInfo
    {
        get;
    }

    /// <summary>Whether the process with the recorded ID and start time is still alive; throws if the identity was not captured.</summary>
    public bool IsRunning
    {
        get
        {
            if (!_pid.HasValue || !_startTimeUtc.HasValue)
            {
                throw new InvalidOperationException("The child process has not reported its identity.");
            }

            try
            {
                using var child = Process.GetProcessById(_pid.Value);
                return IsSameChild(child);
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or Win32Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Waits up to 30 seconds for the child to write its PID file, then captures its identity.</summary>
    public async Task WaitUntilReady(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var pidText = string.Empty;
        while (string.IsNullOrWhiteSpace(pidText))
        {
            if (File.Exists(_pidFile))
            {
                pidText = await File.ReadAllTextAsync(_pidFile, timeout.Token);
            }

            if (string.IsNullOrWhiteSpace(pidText))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }

        var pid = int.Parse(pidText, NumberStyles.None, CultureInfo.InvariantCulture);
        if (!TryCaptureIdentity(pid))
        {
            throw new InvalidOperationException("The child process exited before its identity could be captured.");
        }
    }

    /// <summary>Kills the child's process tree if it is still the recorded process, then deletes the PID file.</summary>
    public async ValueTask DisposeAsync()
    {
        await CapturePidIfAvailable();
        if (_pid.HasValue && _startTimeUtc.HasValue)
        {
            try
            {
                using var child = Process.GetProcessById(_pid.Value);
                if (IsSameChild(child))
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync(CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException
                or Win32Exception
                or TimeoutException)
            {
                // The child is already gone or could not be reaped during best-effort test cleanup.
            }
        }

        try
        {
            File.Delete(_pidFile);
        }
        catch (IOException)
        {
            // Best-effort cleanup for a test-only readiness file.
        }
    }

    /// <summary>Returns whether the process has not exited and started at the given UTC time, which tells a reused process ID from the original child.</summary>
    internal static bool MatchesIdentity(Process child, DateTime startTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(child);
        return !child.HasExited
            && child.StartTime.ToUniversalTime() == startTimeUtc;
    }

    private async Task CapturePidIfAvailable()
    {
        if (_pid.HasValue || !File.Exists(_pidFile))
        {
            return;
        }

        try
        {
            var pidText = await File.ReadAllTextAsync(_pidFile, CancellationToken.None);
            if (int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPid))
            {
                _ = TryCaptureIdentity(parsedPid);
            }
        }
        catch (IOException)
        {
            // The child may have been interrupted while writing its PID.
        }
    }

    private bool TryCaptureIdentity(int pid)
    {
        try
        {
            using var child = Process.GetProcessById(pid);
            if (child.HasExited)
            {
                return false;
            }

            _pid = pid;
            _startTimeUtc = child.StartTime.ToUniversalTime();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or Win32Exception)
        {
            return false;
        }
    }

    private bool IsSameChild(Process child)
    {
        return _startTimeUtc.HasValue
            && MatchesIdentity(child, _startTimeUtc.Value);
    }
}
