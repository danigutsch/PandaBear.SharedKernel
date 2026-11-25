using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using SharedKernel.Processes;

namespace SharedKernel.PlaywrightMcp;

/// <summary>
/// Runs the digest-pinned Playwright MCP image as a stdio MCP server on a local Docker engine or rootless Podman, and pulls
/// or removes that image. Every operation first selects the engine from the settings, refuses remote engine overrides, and
/// checks that the engine is local, then returns an exit code and writes diagnostics to the given writer.
/// </summary>
public sealed class PlaywrightMcpServer
{
    private readonly IProcessRunner _processRunner;
    private readonly IExecutableResolver _executableResolver;

    /// <summary>Creates the server from the process runner it starts the container engine with and the resolver that finds the engine.</summary>
    /// <param name="processRunner">Runs the container engine, such as <see cref="SystemProcessRunner"/>.</param>
    /// <param name="executableResolver">Finds <c>docker</c> and <c>podman</c>, such as <see cref="PathExecutableResolver.Default"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
    public PlaywrightMcpServer(IProcessRunner processRunner, IExecutableResolver executableResolver)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(executableResolver);
        _processRunner = processRunner;
        _executableResolver = executableResolver;
    }

    /// <summary>Gets the digest-pinned Playwright MCP image reference that every command uses.</summary>
    public static string Image => PlaywrightMcpContainerArguments.Image;

    /// <summary>
    /// Starts the Playwright MCP container server on standard input and output and returns its exit code: the server's
    /// exit code, 2 for an invalid environment, or 1 for an engine, I/O, cleanup, or cancellation failure.
    /// </summary>
    /// <param name="settings">The engine selection and container options.</param>
    /// <param name="error">Receives diagnostics, and the engine's own output for <c>prepare</c> and <c>clean</c>.</param>
    /// <param name="ct">Cancels the operation and stops the container engine.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Serve(PlaywrightMcpSettings settings, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(error);
        return Run(settings, error, RunServer, ct);
    }

    /// <summary>Pulls the Playwright MCP container image and returns the engine's exit code, 2 for an invalid environment, or 1 for a failure.</summary>
    /// <param name="settings">The engine selection and container options.</param>
    /// <param name="error">Receives diagnostics, and the engine's own output for <c>prepare</c> and <c>clean</c>.</param>
    /// <param name="ct">Cancels the operation and stops the container engine.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Prepare(PlaywrightMcpSettings settings, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(error);
        return Run(
            settings,
            error,
            (options, token) => RunToExit(PlaywrightMcpContainerArguments.CreatePullStartInfo(options), error, token),
            ct);
    }

    /// <summary>Removes the Playwright MCP container image and returns the engine's exit code, 2 for an invalid environment, or 1 for a failure.</summary>
    /// <param name="settings">The engine selection and container options.</param>
    /// <param name="error">Receives diagnostics, and the engine's own output for <c>prepare</c> and <c>clean</c>.</param>
    /// <param name="ct">Cancels the operation and stops the container engine.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    public Task<int> Clean(PlaywrightMcpSettings settings, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(error);
        return Run(
            settings,
            error,
            (options, token) => RunToExit(PlaywrightMcpContainerArguments.CreateImageRemoveStartInfo(options), error, token),
            ct);
    }

    private async Task<int> Run(
        PlaywrightMcpSettings settings,
        TextWriter error,
        Func<PlaywrightMcpRuntime, CancellationToken, Task<int>> operation,
        CancellationToken ct)
    {
        try
        {
            var options = PlaywrightMcpRuntime.Resolve(settings, _executableResolver);
            ct.ThrowIfCancellationRequested();
            options = await ValidateLocalEngine(options, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            return await operation(options, ct).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync($"Error: {exception.Message}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync($"Error: {exception.Message}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
        catch (Win32Exception exception)
        {
            await error.WriteLineAsync($"Error: Could not start Playwright MCP: {exception.Message}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
        catch (IOException exception)
        {
            await error.WriteLineAsync($"Error: Playwright MCP process I/O failed: {exception.Message}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Error: Playwright MCP was cancelled.".AsMemory(), CancellationToken.None).ConfigureAwait(false);
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
    private async Task<ProcessOutput> RunEngine(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken ct)
    {
        ProcessOutput result;
        try
        {
            result = await _processRunner.Run(startInfo, forward, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("Playwright MCP was cancelled.", exception, ct);
        }

        ct.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<int> RunServer(PlaywrightMcpRuntime options, CancellationToken ct)
    {
        var containerName = $"sharedkernel-playwright-mcp-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var startInfo = PlaywrightMcpContainerArguments.CreateRuntimeStartInfo(options, containerName);
        ProcessOutput? runtimeResult = null;
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
        return runtimeResult!.ExitCode;
    }

    private async Task<PlaywrightMcpRuntime> ValidateLocalEngine(
        PlaywrightMcpRuntime options,
        CancellationToken ct)
    {
        if (options.Engine == ContainerEngine.Docker)
        {
            var contextResult = await RunEngine(
                PlaywrightMcpContainerArguments.CreateDockerContextShowStartInfo(options),
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
                PlaywrightMcpContainerArguments.CreateDockerContextProbeStartInfo(options),
                null,
                ct).ConfigureAwait(false);
            if (probeResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Docker context inspection failed: {probeResult.StandardError.Trim()}");
            }

            var dockerEndpoint = probeResult.StandardOutput.Trim();
            if (!PlaywrightMcpContainerArguments.IsLocalDockerEndpoint(dockerEndpoint))
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
            PlaywrightMcpContainerArguments.CreatePodmanRootlessProbeStartInfo(options),
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

    private async Task CleanupContainer(PlaywrightMcpRuntime options, string containerName)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanup = PlaywrightMcpContainerArguments.CreateContainerCleanupStartInfo(options, containerName);
        try
        {
            var cleanupResult = await _processRunner.Run(cleanup, null, cleanupTimeout.Token).ConfigureAwait(false);
            var cleanupSucceeded = cleanupResult.ExitCode == 0;
            if (cleanupResult.ExitCode != 0 && options.Engine == ContainerEngine.Docker)
            {
                var existenceProbe = PlaywrightMcpContainerArguments.CreateContainerExistenceProbeStartInfo(
                    options,
                    containerName);
                var existenceResult = await _processRunner.Run(
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
}
