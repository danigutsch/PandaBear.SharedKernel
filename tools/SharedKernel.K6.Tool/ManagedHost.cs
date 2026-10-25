using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PandaBear.K6.Tool;

/// <summary>
/// A process that serves the k6 target for the duration of a run. It writes its base URL to a ready file and answers
/// a health path; its output goes to log files in the directory of the run.
/// </summary>
internal sealed class ManagedHost : IAsyncDisposable
{
    /// <summary>The argument that becomes the address the host binds.</summary>
    public const string ListenUrlPlaceholder = "{listenUrl}";

    private const int MaxReadyFileBytes = 4096;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan HealthAttemptTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan LogDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly Task _logs;
    private readonly string _scenario;
    private readonly K6HostConfiguration _configuration;
    private readonly string _readyFile;
    private readonly string _logHint;

    private ManagedHost(Process process, Task logs, string scenario, K6HostConfiguration configuration, string readyFile, string logHint)
    {
        _process = process;
        _logs = logs;
        _scenario = scenario;
        _configuration = configuration;
        _readyFile = readyFile;
        _logHint = logHint;
    }

    /// <summary>Starts the host in the repository root.</summary>
    /// <param name="executable">The absolute host executable.</param>
    /// <param name="scenario">The scenario name, for errors.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="runDirectory">The absolute directory of the run, which receives the ready file and the logs.</param>
    /// <param name="relativeRunDirectory">The run directory relative to the root, for errors.</param>
    /// <param name="container">Whether k6 runs in a container, so the host binds every address instead of the loopback address.</param>
    /// <exception cref="InvalidOperationException">The host could not start.</exception>
    public static ManagedHost Start(
        string executable,
        string scenario,
        K6HostConfiguration configuration,
        string root,
        string runDirectory,
        string relativeRunDirectory,
        bool container)
    {
        var listenUrl = container ? "http://0.0.0.0:0" : "http://127.0.0.1:0";
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        foreach (var argument in configuration.Arguments)
        {
            startInfo.ArgumentList.Add(argument == ListenUrlPlaceholder ? listenUrl : argument);
        }

        ChildProcess.RemoveTokens(startInfo);
        var readyFile = Path.Combine(runDirectory, "host.url");
        startInfo.Environment[configuration.ReadyFileVariable] = readyFile;
        var process = ChildProcess.Start(startInfo);
        var logs = Task.WhenAll(
            Copy(process.StandardOutput.BaseStream, Path.Combine(runDirectory, "host.stdout.log")),
            Copy(process.StandardError.BaseStream, Path.Combine(runDirectory, "host.stderr.log")));
        var logHint = $"See {relativeRunDirectory}/host.stdout.log and {relativeRunDirectory}/host.stderr.log.";
        return new ManagedHost(process, logs, scenario, configuration, readyFile, logHint);
    }

    /// <summary>Waits for the ready file and then for a success status from the health path.</summary>
    /// <param name="allowExternal">Whether the host may report a base URL other than a loopback host.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The validated base URL, with an unspecified address replaced by the loopback address.</returns>
    /// <exception cref="InvalidOperationException">The host exited, reported an invalid base URL, or timed out.</exception>
    public async Task<Uri> WaitUntilReady(bool allowExternal, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        string? value;
        while ((value = await ReadReadyFile(ct).ConfigureAwait(false)) is null)
        {
            ThrowIfExited();
            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(_configuration.ReadyTimeoutSeconds))
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The host of scenario {_scenario} did not write {_configuration.ReadyFileVariable} within {_configuration.ReadyTimeoutSeconds} seconds. {_logHint}"));
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        var target = K6Target.Parse(K6Target.ToLoopback(value), _configuration.ReadyFileVariable, allowExternal);
        await WaitForHealth(new Uri(K6Target.Format(target) + _configuration.HealthPath, UriKind.Absolute), ct).ConfigureAwait(false);
        return target;
    }

    /// <summary>Stops the host process tree and finishes its logs.</summary>
    public async ValueTask DisposeAsync()
    {
        await ChildProcess.Terminate(_process).ConfigureAwait(false);
        try
        {
            await _logs.WaitAsync(LogDrainTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // A descendant that outlived the kill can hold the output open; the logs keep what arrived.
        }

        _process.Dispose();
    }

    private async Task WaitForHealth(Uri health, CancellationToken ct)
    {
        // The probe reaches only the host: no proxy, and a redirect is not followed.
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            ThrowIfExited();
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                attempt.CancelAfter(HealthAttemptTimeout);
                try
                {
                    using var response = await client.GetAsync(health, HttpCompletionOption.ResponseHeadersRead, attempt.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException
                    || (exception is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    // The host can write its base URL before it accepts connections.
                }
            }

            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(_configuration.HealthTimeoutSeconds))
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The host of scenario {_scenario} did not answer {health} with a success status within {_configuration.HealthTimeoutSeconds} seconds. {_logHint}"));
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<string?> ReadReadyFile(CancellationToken ct)
    {
        try
        {
            var stream = new FileStream(_readyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            await using (stream.ConfigureAwait(false))
            {
                var buffer = new byte[MaxReadyFileBytes];
                var length = 0;
                int read;
                while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), ct).ConfigureAwait(false)) > 0)
                {
                    length += read;
                }

                var value = Encoding.UTF8.GetString(buffer, 0, length).Trim();
                return value.Length == 0 ? null : value;
            }
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private void ThrowIfExited()
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The host of scenario {_scenario} exited with code {_process.ExitCode} before it was ready. {_logHint}"));
        }
    }

    private static async Task Copy(Stream source, string path)
    {
        var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await using (destination.ConfigureAwait(false))
        {
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }
    }
}
