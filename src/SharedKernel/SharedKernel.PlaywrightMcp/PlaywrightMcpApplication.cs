using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using PandaBear.Tooling;

namespace PandaBear.PlaywrightMcp;

/// <summary>
/// Runs the pinned Playwright MCP container image against a local Docker or rootless Podman engine. The environment,
/// executable lookup, and process runner are injected, so a host supplies them and tests replace them.
/// </summary>
public sealed class PlaywrightMcpApplication
{
    private readonly Func<string, string?> getEnvironmentVariable;
    private readonly Func<string, string?> resolveExecutable;
    private readonly Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessResult>> runProcess;

    /// <summary>Creates the application from the environment it reads and the process runner it starts engines with.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable by name, such as <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <param name="resolveExecutable">Finds an executable by command name, such as <see cref="ResolveExecutable"/>.</param>
    /// <param name="runProcess">Starts a process and returns its result, such as <see cref="RunProcess"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getEnvironmentVariable"/>, <paramref name="resolveExecutable"/>, or <paramref name="runProcess"/> is <see langword="null"/>.</exception>
    public PlaywrightMcpApplication(
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable,
        Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessResult>> runProcess)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        ArgumentNullException.ThrowIfNull(runProcess);
        this.getEnvironmentVariable = getEnvironmentVariable;
        this.resolveExecutable = resolveExecutable;
        this.runProcess = runProcess;
    }

    /// <summary>Gets the digest-pinned Playwright MCP image reference that every command uses.</summary>
    public static string Image => PlaywrightMcpCommand.Image;

    /// <summary>
    /// Starts the Playwright MCP container server on standard input and output and returns its exit code: the server's
    /// exit code, 2 for an invalid environment, or 1 for an engine, I/O, cleanup, or cancellation failure.
    /// </summary>
    /// <param name="error">Writer for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Serve(TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Run(error, RunServer, ct);
    }

    /// <summary>Pulls the Playwright MCP container image and returns the engine's exit code, 2 for an invalid environment, or 1 for a failure.</summary>
    /// <param name="error">Writer for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Prepare(TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Run(
            error,
            (options, token) => RunToExit(PlaywrightMcpCommand.CreatePullStartInfo(options), error, token),
            ct);
    }

    /// <summary>Removes the Playwright MCP container image and returns the engine's exit code, 2 for an invalid environment, or 1 for a failure.</summary>
    /// <param name="error">Writer for diagnostics.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Clean(TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Run(
            error,
            (options, token) => RunToExit(PlaywrightMcpCommand.CreateImageRemoveStartInfo(options), error, token),
            ct);
    }

    private async Task<int> Run(
        TextWriter error,
        Func<PlaywrightMcpOptions, CancellationToken, Task<int>> operation,
        CancellationToken ct)
    {
        try
        {
            var options = PlaywrightMcpOptions.Parse(getEnvironmentVariable, resolveExecutable);
            ct.ThrowIfCancellationRequested();
            options = await ValidateLocalEngine(options, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            return await operation(options, ct).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync($"Error: {exception.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync($"Error: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (Win32Exception exception)
        {
            await error.WriteLineAsync($"Error: Could not start Playwright MCP: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (IOException exception)
        {
            await error.WriteLineAsync($"Error: Playwright MCP process I/O failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Error: Playwright MCP was cancelled.").ConfigureAwait(false);
            return 1;
        }
    }

    // The engine's output and errors are this tool's diagnostics, so each line goes to standard error as it arrives.
    private async Task<int> RunToExit(ProcessStartInfo startInfo, TextWriter error, CancellationToken ct)
    {
        var result = await RunEngine(startInfo, error, ct).ConfigureAwait(false);
        return result.ExitCode;
    }

    // Ctrl+C and SIGTERM sent to the process group also stop the container engine, which can exit or fail before the
    // runner observes the token. Either outcome then belongs to the cancellation.
    private async Task<ProcessResult> RunEngine(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken ct)
    {
        ProcessResult result;
        try
        {
            result = await runProcess(startInfo, forward, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("Playwright MCP was cancelled.", exception, ct);
        }

        ct.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<int> RunServer(PlaywrightMcpOptions options, CancellationToken ct)
    {
        var containerName = $"sharedkernel-playwright-mcp-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var startInfo = PlaywrightMcpCommand.CreateRuntimeStartInfo(options, containerName);
        ProcessResult runtimeResult = default;
        ExceptionDispatchInfo? runtimeFailure = null;
        try
        {
            runtimeResult = await RunEngine(startInfo, null, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or OperationCanceledException
            or IOException)
        {
            runtimeFailure = ExceptionDispatchInfo.Capture(exception);
        }

        await CleanupContainer(options, containerName).ConfigureAwait(false);

        runtimeFailure?.Throw();
        return runtimeResult.ExitCode;
    }

    private async Task<PlaywrightMcpOptions> ValidateLocalEngine(
        PlaywrightMcpOptions options,
        CancellationToken ct)
    {
        if (options.Engine == ContainerEngine.Docker)
        {
            var contextResult = await RunEngine(
                PlaywrightMcpCommand.CreateDockerContextShowStartInfo(options),
                null,
                ct).ConfigureAwait(false);
            if (contextResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Docker current-context inspection failed: {contextResult.StandardError.Trim()}");
            }

            var dockerContext = contextResult.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(dockerContext)
                || dockerContext.Contains('\r', StringComparison.Ordinal)
                || dockerContext.Contains('\n', StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Docker returned an invalid current context name.");
            }

            options = options with
            {
                DockerContext = dockerContext
            };
            var probeResult = await RunEngine(
                PlaywrightMcpCommand.CreateDockerContextProbeStartInfo(options),
                null,
                ct).ConfigureAwait(false);
            if (probeResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Docker context inspection failed: {probeResult.StandardError.Trim()}");
            }

            var dockerEndpoint = probeResult.StandardOutput.Trim();
            if (!PlaywrightMcpCommand.IsLocalDockerEndpoint(dockerEndpoint))
            {
                throw new InvalidOperationException(
                    $"Refusing non-local Docker context '{dockerContext}': {dockerEndpoint}");
            }

            return options with
            {
                DockerEndpoint = dockerEndpoint
            };
        }

        var podmanProbeResult = await RunEngine(
            PlaywrightMcpCommand.CreatePodmanRootlessProbeStartInfo(options),
            null,
            ct).ConfigureAwait(false);
        if (podmanProbeResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Podman rootless inspection failed: {podmanProbeResult.StandardError.Trim()}");
        }

        if (!string.Equals(podmanProbeResult.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Playwright MCP requires local rootless Podman mode.");
        }

        return options;
    }

    private async Task CleanupContainer(PlaywrightMcpOptions options, string containerName)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanup = PlaywrightMcpCommand.CreateContainerCleanupStartInfo(options, containerName);
        try
        {
            var cleanupResult = await runProcess(cleanup, null, cleanupTimeout.Token).ConfigureAwait(false);
            var cleanupSucceeded = cleanupResult.ExitCode == 0;
            if (cleanupResult.ExitCode != 0 && options.Engine == ContainerEngine.Docker)
            {
                var existenceProbe = PlaywrightMcpCommand.CreateContainerExistenceProbeStartInfo(
                    options,
                    containerName);
                var existenceResult = await runProcess(
                    existenceProbe,
                    null,
                    cleanupTimeout.Token).ConfigureAwait(false);
                cleanupSucceeded = existenceResult.ExitCode == 0
                    && string.IsNullOrWhiteSpace(existenceResult.StandardOutput);
                if (!cleanupSucceeded)
                {
                    throw new InvalidOperationException(
                        $"Container cleanup failed for '{containerName}': {cleanupResult.StandardError.Trim()} "
                        + $"Removal check: {existenceResult.StandardError.Trim()}");
                }
            }

            if (!cleanupSucceeded)
            {
                throw new InvalidOperationException(
                    $"Container cleanup failed for '{containerName}': {cleanupResult.StandardError.Trim()}");
            }
        }
        catch (OperationCanceledException exception)
        {
            throw new InvalidOperationException(
                $"Container cleanup timed out for '{containerName}'. Remove it manually.",
                exception);
        }
    }

    /// <summary>Runs a process and kills its process tree when <paramref name="ct"/> is canceled.</summary>
    /// <param name="startInfo">The process to run.</param>
    /// <param name="forward">
    /// Receives each line of the redirected standard output and standard error as it arrives, and the result then holds
    /// no output; <see langword="null"/> collects the output in the result.
    /// </param>
    /// <param name="ct">Cancels the run and kills the process tree.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="startInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The process could not be started.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled; the process tree has been killed.</exception>
    public static async Task<ProcessResult> RunProcess(
        ProcessStartInfo startInfo,
        TextWriter? forward,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        using var process = new Process { StartInfo = startInfo };
        ct.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        }

        // The two streams are read concurrently, and a TextWriter is not thread-safe, so their lines share one lock.
        Func<string, Task>? writeLine = forward is null ? null : TextWriter.Synchronized(forward).WriteLineAsync;
        var standardOutput = startInfo.RedirectStandardOutput
            ? Read(process.StandardOutput, writeLine)
            : Task.FromResult(string.Empty);
        var standardError = startInfo.RedirectStandardError
            ? Read(process.StandardError, writeLine)
            : Task.FromResult(string.Empty);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    using var terminationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(terminationTimeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or AggregateException
                or OperationCanceledException)
            {
                // Preserve cancellation so the caller can force-remove the named container.
            }

            var outputDrain = Task.WhenAll(standardOutput, standardError);
            try
            {
                await outputDrain.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException
                or IOException
                or InvalidOperationException)
            {
                _ = outputDrain.ContinueWith(
                    static completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static async Task<string> Read(StreamReader reader, Func<string, Task>? writeLine)
    {
        if (writeLine is null)
        {
            return await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            await writeLine(line).ConfigureAwait(false);
        }

        return string.Empty;
    }

    /// <summary>Resolves a command to an executable path using the <c>PATH</c> environment variable, or returns <see langword="null" /> when it is not found.</summary>
    public static string? ResolveExecutable(string command) =>
        ExecutableResolver.Resolve(command, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows());
}
