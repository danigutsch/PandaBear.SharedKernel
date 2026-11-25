using System.Diagnostics;
using System.Globalization;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.CI;

/// <summary>
/// A slice's service while its tests run. The tool starts it with <c>dotnet run</c> and redirected standard input, output, and
/// error, waits for the ready file in which it writes its values, deletes that file as soon as it has read them, and stops
/// it by closing its standard input, which it reads as end-of-file. Its output goes straight to log files in the execution
/// directory. The ready file lives in a private temporary directory, because the execution directory is uploaded as a CI
/// artifact.
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
    private readonly Func<string, CancellationToken, Task<byte[]>> _readReadyFile;
    private readonly string _logHint;
    private bool _exitReported;

    private ServiceHost(
        string slice,
        Process process,
        Task logs,
        ServiceConfiguration configuration,
        DirectoryInfo directory,
        string readyFile,
        Func<string, CancellationToken, Task<byte[]>> readReadyFile,
        string logHint)
    {
        Slice = slice;
        _process = process;
        _logs = logs;
        _configuration = configuration;
        _directory = directory;
        _readyFile = readyFile;
        _readReadyFile = readReadyFile;
        _logHint = logHint;
    }

    /// <summary>Gets the slice whose tests the service serves.</summary>
    public string Slice { get; }

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
    /// <param name="readReadyFile">
    /// Reads the ready file at the path it is given, or throws <see cref="FileNotFoundException"/> when the file is not there yet; by default
    /// a bounded read that refuses a file larger than 4 KiB. A test passes its own to decide when the service's writes happen.
    /// </param>
    /// <exception cref="InvalidOperationException">The process could not start.</exception>
    public static ServiceHost Start(
        string slice,
        ServiceConfiguration configuration,
        string buildConfiguration,
        string root,
        string execution,
        Func<ProcessStartInfo, Process> start,
        Func<string, CancellationToken, Task<byte[]>>? readReadyFile = null)
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
                readReadyFile ?? ((path, ct) => BoundedFile.Read(path, MaxValueBytes, $"The ready file of the service of slice {slice}", ct)),
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
    /// ready file in the environment. The environment has no GitHub token, and none of the value variables, because the service creates
    /// what the values stand for and must not borrow them.
    /// </summary>
    internal static ProcessStartInfo CreateStartInfo(ServiceConfiguration configuration, string buildConfiguration, string root, string readyFile)
    {
        var startInfo = new ProcessStartInfo(PathExecutableResolver.Default.Resolve("dotnet"))
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

        ProcessEnvironment.RemoveForgeTokens(startInfo);
        foreach (var variable in configuration.ValueVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        startInfo.Environment[configuration.ReadyFileVariable] = readyFile;
        return startInfo;
    }

    /// <summary>
    /// Reads the values from the text of the ready file, after a byte order mark: one <c>NAME=value</c> line for each of
    /// <paramref name="variables"/> in any order, where <c>NAME</c> is the variable exactly and the value, without surrounding
    /// whitespace, is not empty and holds no control character. No other line is allowed.
    /// </summary>
    /// <param name="text">The text of the ready file.</param>
    /// <param name="variables">The value variables of the service.</param>
    /// <param name="slice">The slice that owns the service.</param>
    /// <returns>
    /// The variables with their values, in the order of <paramref name="variables"/>, or <see langword="null"/> when the text is only
    /// white space, which is a file whose service has not written it yet.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// A line names no value variable or repeats one, a value is empty or holds a control character, or a value variable is missing. The
    /// message names the line and the variable, never a value, and never what a line that names no variable holds.
    /// </exception>
    internal static IReadOnlyList<(string Variable, string Value)>? ParseValues(string text, IReadOnlyList<string> variables, string slice)
    {
        var content = text.TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var prefix = $"The ready file of the service of slice {slice} must hold one NAME=value line for each of its value variables ({string.Join(", ", variables)}); ";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(content);
        var number = 0;
        while (reader.ReadLine() is { } line)
        {
            number++;
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            var variable = (separator < 0 ? null : variables.FirstOrDefault(candidate => string.Equals(candidate, line[..separator], StringComparison.Ordinal)))
                ?? throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{prefix}line {number} names none of them."));

            var value = line[(separator + 1)..].Trim();
            if (value.Length == 0)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{prefix}line {number} gives {variable} no value."));
            }

            if (value.Any(char.IsControl))
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{prefix}line {number} gives {variable} a value with a control character."));
            }

            if (!values.TryAdd(variable, value))
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{prefix}line {number} repeats {variable}."));
            }
        }

        var missing = variables.Where(variable => !values.ContainsKey(variable)).ToArray();
        return missing.Length > 0
            ? throw new InvalidDataException(prefix + "it lacks " + string.Join(", ", missing) + ".")
            : [.. variables.Select(variable => (variable, values[variable]))];
    }

    /// <summary>Describes the started service and where its output goes.</summary>
    public string ReadyMessage() =>
        $"The service of slice {Slice} is ready: {_configuration.Project}{Environment.NewLine}Output: {StandardOutputLog}, {StandardErrorLog}{Environment.NewLine}";

    /// <summary>
    /// Waits for the service to write its values to the ready file. Each poll notes whether the service has exited before it reads the
    /// file, so a service that wrote its values and then exited is ready, and an exit fails the wait only when the read that followed it
    /// found no values.
    /// </summary>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The value variables with their values, in the order of the configuration; a value is never part of a message.</returns>
    /// <exception cref="InvalidOperationException">The service exited, or did not write its values in time.</exception>
    /// <exception cref="InvalidDataException">The ready file is larger than 4 KiB or does not hand over each value variable once.</exception>
    public async Task<IReadOnlyList<(string Variable, string Value)>> WaitUntilReady(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(_configuration.ReadyTimeoutSeconds);
        while (true)
        {
            var exited = _process.HasExited;
            if (await ReadValues(ct).ConfigureAwait(false) is { } values)
            {
                DeleteReadyFile();
                return values;
            }

            if (exited)
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
            await ProcessTree.Terminate(_process, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<(string Variable, string Value)>?> ReadValues(CancellationToken ct)
    {
        byte[] content;
        try
        {
            content = await _readReadyFile(_readyFile, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        return ParseValues(Encoding.UTF8.GetString(content), _configuration.ValueVariables, Slice);
    }

    // The values are in memory from here on, and the file is the only place on disk that holds them, so a run that is cut short,
    // even killed, leaves an empty directory and not a credential.
    private void DeleteReadyFile()
    {
        try
        {
            File.Delete(_readyFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Stop deletes the whole directory and reports it when it stays, so a file that resists here is not forgotten.
        }
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
