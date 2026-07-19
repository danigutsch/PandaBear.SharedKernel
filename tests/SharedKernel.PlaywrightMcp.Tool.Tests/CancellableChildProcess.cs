using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SharedKernel.PlaywrightMcp.Tool.Tests;

internal sealed class CancellableChildProcess : IAsyncDisposable
{
    private readonly string _pidFile = Path.Combine(
        Path.GetTempPath(),
        $"sharedkernel-playwright-mcp-{Guid.NewGuid():N}.pid");
    private int? _pid;
    private DateTime? _startTimeUtc;

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
                "Set-Content -LiteralPath $env:PID_FILE -Value $PID -NoNewline; Start-Sleep -Seconds 300");
        }
        else
        {
            StartInfo.FileName = "/bin/sh";
            StartInfo.ArgumentList.Add("-c");
            StartInfo.ArgumentList.Add("printf %s \"$$\" > \"$PID_FILE\"; exec sleep 300");
        }

        StartInfo.Environment["PID_FILE"] = _pidFile;
    }

    public ProcessStartInfo StartInfo { get; }

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

    public async Task WaitUntilReady(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
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
