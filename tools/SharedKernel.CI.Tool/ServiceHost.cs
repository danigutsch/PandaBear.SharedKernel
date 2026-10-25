using System.Diagnostics;
using System.Globalization;
using System.Text;
using PandaBear.Tooling;

namespace PandaBear.CI.Tool;

/// <summary>
/// A slice's service while its tests run. The tool starts it with <c>dotnet run</c> and redirected standard input, output, and
/// error, waits for the ready file in which it writes its value, and stops it by closing its standard input, which it reads as
/// end-of-file. Its output goes straight to log files in the execution directory. The ready file lives in a private
/// temporary directory, because the execution directory is uploaded as a CI artifact.
/// </summary>
internal sealed class ServiceHost
{
    private const string TempPrefix = "sharedkernel-ci-service-";

    private const string ReadyFileName = "ready";

    private const int MaxValueBytes = 4096;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan LogDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly Task _logs;
    private readonly ServiceConfiguration _configuration;
    private readonly DirectoryInfo _directory;
    private readonly string _readyFile;
    private readonly string _logHint;
    private bool _exitReported;

    private ServiceHost(string slice, Process process, Task logs, ServiceConfiguration configuration, DirectoryInfo directory, string readyFile, string logHint)
    {
        Slice = slice;
        _process = process;
        _logs = logs;
        _configuration = configuration;
        _directory = directory;
        _readyFile = readyFile;
        _logHint = logHint;
    }

    /// <summary>Gets the slice whose tests the service serves.</summary>
    public string Slice
    {
        get;
    }

    private string StandardOutputLog => PhaseName(Slice) + ".stdout.log";

    private string StandardErrorLog => PhaseName(Slice) + ".stderr.log";

    /// <summary>Returns the name of the phase that starts the service of <paramref name="slice"/> and waits for it.</summary>
    public static string PhaseName(string slice) => "service-" + slice;

    /// <summary>Returns the name of the phase that stops the service of <paramref name="slice"/>.</summary>
    public static string StopPhaseName(string slice) => "service-stop-" + slice;

    /// <summary>Starts the service from the repository root.</summary>
    /// <param name="slice">The slice that owns the service.</param>
    /// <param name="configuration">The service configuration.</param>
    /// <param name="buildConfiguration">The configuration of the build that <c>dotnet run</c> uses.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="execution">The absolute execution directory, which receives the logs.</param>
    /// <param name="start">Starts the process from its start information.</param>
    /// <exception cref="InvalidOperationException">The process could not start.</exception>
    public static ServiceHost Start(
        string slice,
        ServiceConfiguration configuration,
        string buildConfiguration,
        string root,
        string execution,
        Func<ProcessStartInfo, Process> start)
    {
        // The directory is created before the process, so the process never finds its ready file's directory missing.
        var directory = Directory.CreateTempSubdirectory(TempPrefix);
        var started = false;
        try
        {
            var readyFile = Path.Combine(directory.FullName, ReadyFileName);
            var process = start(CreateStartInfo(configuration, buildConfiguration, root, readyFile));
            var name = PhaseName(slice);
            var logs = Task.WhenAll(
                Copy(process.StandardOutput.BaseStream, Path.Combine(execution, name + ".stdout.log")),
                Copy(process.StandardError.BaseStream, Path.Combine(execution, name + ".stderr.log")));
            var relative = Path.GetRelativePath(root, execution).Replace('\\', '/');
            started = true;
            return new ServiceHost(
                slice,
                process,
                logs,
                configuration,
                directory,
                readyFile,
                $"See {relative}/{name}.stdout.log and {relative}/{name}.stderr.log.");
        }
        finally
        {
            if (!started)
            {
                directory.Delete(recursive: true);
            }
        }
    }

    /// <summary>
    /// Creates the start information of <c>dotnet run</c> for the built service project, with all three streams redirected and the
    /// ready file in the environment. The environment has no GitHub token, and no value variable, because the service creates what the
    /// value stands for and must not borrow it.
    /// </summary>
    internal static ProcessStartInfo CreateStartInfo(ServiceConfiguration configuration, string buildConfiguration, string root, string readyFile)
    {
        var startInfo = new ProcessStartInfo(ExecutableResolver.ResolveCommand("dotnet"))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        string[] arguments = ["run", "--project", configuration.Project, "-c", buildConfiguration, "--no-build", "--no-restore", "--no-launch-profile"];
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        RemoveTokens(startInfo);
        startInfo.Environment.Remove(configuration.ValueVariable);
        startInfo.Environment[configuration.ReadyFileVariable] = readyFile;
        return startInfo;
    }

    /// <summary>Removes every GitHub token variable, in any case, from the child environment.</summary>
    internal static void RemoveTokens(ProcessStartInfo startInfo)
    {
        foreach (var name in startInfo.Environment.Keys.Where(CiConfigurationFile.IsTokenVariable).ToArray())
        {
            startInfo.Environment.Remove(name);
        }
    }

    /// <summary>
    /// Reads the value from the text of the ready file: its first line without a byte order mark or surrounding whitespace.
    /// </summary>
    /// <returns>The value, or <see langword="null"/> when the first line is empty.</returns>
    /// <exception cref="InvalidDataException">The value contains a control character, so it cannot travel in an environment variable.</exception>
    internal static string? ParseValue(string text, string slice)
    {
        var line = new StringReader(text).ReadLine()?.TrimStart('\uFEFF').Trim() ?? string.Empty;
        if (line.Length == 0)
        {
            return null;
        }

        return line.Any(char.IsControl)
            ? throw new InvalidDataException($"The ready file of the service of slice {slice} must hold one line without control characters.")
            : line;
    }

    /// <summary>Describes the started service and where its output goes.</summary>
    public string ReadyMessage() =>
        $"The service of slice {Slice} is ready: {_configuration.Project}{Environment.NewLine}Output: {StandardOutputLog}, {StandardErrorLog}{Environment.NewLine}";

    /// <summary>Waits for the service to write its value to the ready file.</summary>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The value, which is never part of a message.</returns>
    /// <exception cref="InvalidOperationException">The service exited, or did not write a value in time.</exception>
    public async Task<string> WaitUntilReady(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(_configuration.ReadyTimeoutSeconds);
        while (true)
        {
            if (await ReadValue(ct).ConfigureAwait(false) is { } value)
            {
                return value;
            }

            if (_process.HasExited)
            {
                _exitReported = true;
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture, $"The service of slice {Slice} exited with code {_process.ExitCode} before it was ready. {_logHint}"));
            }

            if (Stopwatch.GetElapsedTime(started) >= timeout)
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The service of slice {Slice} did not write {_configuration.ReadyFileVariable} within {_configuration.ReadyTimeoutSeconds} seconds. {_logHint}"));
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the service's standard input, waits for it to exit gracefully for as long as its stop timeout allows, kills its
    /// process tree only after that, finishes the logs, and deletes the ready file's directory.
    /// </summary>
    /// <returns>A description of the stop.</returns>
    /// <exception cref="InvalidOperationException">
    /// The service had to be killed, exited with a failure code that no earlier message reported, or left its ready file behind.
    /// </exception>
    public async Task<string> Stop()
    {
        var started = Stopwatch.GetTimestamp();
        var exitedEarly = _process.HasExited;
        CloseInput();
        var stopTimeout = TimeSpan.FromSeconds(_configuration.StopTimeoutSeconds);
        var exited = exitedEarly || await ExitedWithin(stopTimeout).ConfigureAwait(false);
        if (!exited)
        {
            // The tree dies here, so a service that ignores its input does not outlive the run; its containers can.
            await ProcessRunner.Terminate(_process).ConfigureAwait(false);
        }

        await FinishLogs().ConfigureAwait(false);
        var problems = new List<string>();
        if (!exited)
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The service of slice {Slice} did not exit within {_configuration.StopTimeoutSeconds} seconds after its input closed, so its process tree was killed; the containers that it started may remain. {_logHint}"));
        }
        else if (_process.ExitCode != 0 && !(exitedEarly && _exitReported))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The service of slice {Slice} exited with code {_process.ExitCode} {(exitedEarly ? "before it was stopped" : "after its input closed")}. {_logHint}"));
        }

        try
        {
            _directory.Delete(recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing is left to delete.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The ready file holds the value, so a directory that stays is a failure, not a detail.
            problems.Add($"Could not delete {_directory.FullName}, which held the ready file of the service of slice {Slice}.");
        }

        var exitCode = _process.HasExited ? _process.ExitCode.ToString(CultureInfo.InvariantCulture) : "none";
        _process.Dispose();
        return problems.Count > 0
            ? throw new InvalidOperationException(string.Join(Environment.NewLine, problems))
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Stopped the service of slice {Slice} in {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} seconds; exit code {exitCode}.{Environment.NewLine}");
    }

    private async Task<string?> ReadValue(CancellationToken ct)
    {
        byte[] content;
        try
        {
            content = await BoundedFile.Read(_readyFile, MaxValueBytes, $"The ready file of the service of slice {Slice}", ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        return ParseValue(Encoding.UTF8.GetString(content), Slice);
    }

    private void CloseInput()
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The service has exited and its end of the pipe is gone, so there is no one left to tell.
        }
    }

    private async Task<bool> ExitedWithin(TimeSpan timeout)
    {
        using var source = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(source.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task FinishLogs()
    {
        try
        {
            await _logs.WaitAsync(LogDrainTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // A descendant that outlived the service can hold the output open; the logs keep what arrived.
        }
    }

    private static async Task Copy(Stream source, string path)
    {
        var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await using (destination.ConfigureAwait(false))
        {
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }
    }
}
