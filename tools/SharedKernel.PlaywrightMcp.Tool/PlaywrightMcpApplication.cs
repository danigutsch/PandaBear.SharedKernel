using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace SharedKernel.PlaywrightMcp.Tool;

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class PlaywrightMcpApplication
{
    public static Task<int> Run(
        string[] args,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        return Run(
            args,
            error,
            Environment.GetEnvironmentVariable,
            ResolveExecutable,
            RunProcess,
            cancellationToken);
    }

    internal static async Task<int> Run(
        string[] args,
        TextWriter error,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable,
        Func<ProcessStartInfo, CancellationToken, Task<ProcessResult>> runProcess,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        ArgumentNullException.ThrowIfNull(runProcess);

        try
        {
            var options = PlaywrightMcpOptions.Parse(getEnvironmentVariable, resolveExecutable);
            cancellationToken.ThrowIfCancellationRequested();
            if (options.Engine == ContainerEngine.Docker)
            {
                var contextResult = await runProcess(
                    PlaywrightMcpCommand.CreateDockerContextShowStartInfo(options),
                    cancellationToken).ConfigureAwait(false);
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

                options = options with { DockerContext = dockerContext };
                var probe = PlaywrightMcpCommand.CreateDockerContextProbeStartInfo(options);
                var probeResult = await runProcess(probe, cancellationToken).ConfigureAwait(false);
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

                options = options with { DockerEndpoint = dockerEndpoint };
            }
            else
            {
                var probe = PlaywrightMcpCommand.CreatePodmanRootlessProbeStartInfo(options);
                var probeResult = await runProcess(probe, cancellationToken).ConfigureAwait(false);
                if (probeResult.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Podman rootless inspection failed: {probeResult.StandardError.Trim()}");
                }

                if (!string.Equals(probeResult.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Playwright MCP requires local rootless Podman mode.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (args is ["prepare"])
            {
                var result = await runProcess(
                    PlaywrightMcpCommand.CreatePullStartInfo(options),
                    cancellationToken).ConfigureAwait(false);
                return result.ExitCode;
            }

            if (args is ["clean"])
            {
                var result = await runProcess(
                    PlaywrightMcpCommand.CreateImageRemoveStartInfo(options),
                    cancellationToken).ConfigureAwait(false);
                return result.ExitCode;
            }

            var containerName = $"sharedkernel-playwright-mcp-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var startInfo = PlaywrightMcpCommand.CreateRuntimeStartInfo(options, args, containerName);
            ProcessResult runtimeResult = default;
            ExceptionDispatchInfo? runtimeFailure = null;
            try
            {
                runtimeResult = await runProcess(startInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or OperationCanceledException
                or IOException)
            {
                runtimeFailure = ExceptionDispatchInfo.Capture(exception);
            }

            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var cleanup = PlaywrightMcpCommand.CreateContainerCleanupStartInfo(options, containerName);
            try
            {
                var cleanupResult = await runProcess(cleanup, cleanupTimeout.Token).ConfigureAwait(false);
                var cleanupSucceeded = cleanupResult.ExitCode == 0;
                if (cleanupResult.ExitCode != 0 && options.Engine == ContainerEngine.Docker)
                {
                    var existenceProbe = PlaywrightMcpCommand.CreateContainerExistenceProbeStartInfo(
                        options,
                        containerName);
                    var existenceResult = await runProcess(
                        existenceProbe,
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

            runtimeFailure?.Throw();
            return runtimeResult.ExitCode;
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

    private static async Task<ProcessResult> RunProcess(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        cancellationToken.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        }

        var standardOutput = startInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync(CancellationToken.None)
            : Task.FromResult(string.Empty);
        var standardError = startInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync(CancellationToken.None)
            : Task.FromResult(string.Empty);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
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

    private static string? ResolveExecutable(string command)
    {
        return ResolveExecutable(
            command,
            Environment.GetEnvironmentVariable("PATH"),
            OperatingSystem.IsWindows());
    }

    internal static string? ResolveExecutable(string command, string? path, bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fileNames = isWindows
            ? new[] { $"{command}.exe", command }
            : new[] { command };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var absoluteDirectory = directory.Trim('"');
            if (!Path.IsPathFullyQualified(absoluteDirectory))
            {
                continue;
            }

            foreach (var fileName in fileNames)
            {
                var candidate = Path.Combine(absoluteDirectory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
