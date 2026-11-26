using System.Diagnostics;
using SharedKernel.InputNormalization;
using SharedKernel.Processes;

namespace SharedKernel.DevContainers;

/// <summary>Runs a trusted test command with private local rootless Podman storage and networking.</summary>
public static class IsolatedPodman
{
    private const string DefaultNetwork = "podman";
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Starts <paramref name="command"/> with private Podman configuration and cleans up the owned engine before returning.
    /// </summary>
    /// <param name="host">Host details and bounded runner for local Podman probes and cleanup.</param>
    /// <param name="imageStore">Absolute path of an existing overlay image store, mounted as an additional read-only store.</param>
    /// <param name="networkHelper">The installed rootless network helper, <c>pasta</c> or <c>slirp4netns</c>.</param>
    /// <param name="command">The trusted local executable, arguments, working directory, stream settings, and environment.
    /// The wrapper copies those settings for each run and starts the command as the current user without shell execution.
    /// </param>
    /// <param name="runner">Process runner for the test command.</param>
    /// <param name="output">Receives the child's standard output.</param>
    /// <param name="error">Receives the child's standard error and engine diagnostics.</param>
    /// <param name="cancellationToken">Cancels the test and starts bounded cleanup.</param>
    /// <returns>The child's exit code, or 1 when preflight or cleanup fails and the child had no failure.</returns>
    /// <exception cref="ArgumentException">The image-store path, executable path, network helper, or child execution settings are invalid.</exception>
    /// <exception cref="InvalidOperationException">Local rootless Podman or the private engine scope could not be verified.</exception>
    /// <exception cref="IOException">The private configuration could not be created or read.</exception>
    /// <exception cref="UnauthorizedAccessException">The private configuration could not be created or read.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled the test.</exception>
    public static Task<int> Run(
        PodmanDnsHost host,
        string imageStore,
        string networkHelper,
        ProcessStartInfo command,
        IProcessRunner runner,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        Run(host, imageStore, networkHelper, null, command, runner, output, error, cancellationToken);

    /// <summary>
    /// Runs a command with private Podman storage and an optional separate parent for transient engine state.
    /// </summary>
    /// <param name="host">Host details and bounded runner for local Podman probes and cleanup.</param>
    /// <param name="imageStore">Absolute path of an existing overlay image store, mounted as an additional read-only store.</param>
    /// <param name="networkHelper">The installed rootless network helper.</param>
    /// <param name="runtimeParent">
    /// An existing absolute directory in which the wrapper creates a private runtime directory, or <see langword="null"/>
    /// to keep runtime state alongside the private overlay graph. The wrapper owns only its generated subdirectory.
    /// </param>
    /// <param name="command">The trusted local executable, arguments, working directory, stream settings, and environment.</param>
    /// <param name="runner">Process runner for the test command.</param>
    /// <param name="output">Receives the child's standard output.</param>
    /// <param name="error">Receives the child's standard error and engine diagnostics.</param>
    /// <param name="cancellationToken">Cancels the test and starts bounded cleanup.</param>
    /// <returns>The child's exit code, or 1 when preflight or cleanup fails and the child had no failure.</returns>
    /// <exception cref="ArgumentNullException">A required host, command, runner or writer is null.</exception>
    /// <exception cref="ArgumentException">An engine path, network helper or child execution setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">Local rootless Podman or the private engine scope could not be verified.</exception>
    /// <exception cref="IOException">The private configuration could not be created or read.</exception>
    /// <exception cref="UnauthorizedAccessException">The private configuration could not be created or read.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled the test.</exception>
    public static async Task<int> Run(
        PodmanDnsHost host,
        string imageStore,
        string networkHelper,
        string? runtimeParent,
        ProcessStartInfo command,
        IProcessRunner runner,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(host.RunProcess);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(host.ProcRoot);
        if (!host.IsLinux || host.PodmanExecutable is null || !Path.IsPathFullyQualified(host.PodmanExecutable))
        {
            throw new InvalidOperationException("An isolated engine requires local rootless Podman on Linux.");
        }
        if (!Path.IsPathFullyQualified(imageStore) || !Directory.Exists(imageStore) || !Path.IsPathFullyQualified(command.FileName))
        {
            throw new ArgumentException("An isolated engine requires an existing absolute image-store path and an absolute executable path.");
        }
        if (networkHelper is not ("pasta" or "slirp4netns"))
        {
            throw new ArgumentException("Use pasta or slirp4netns as the rootless network helper.", nameof(networkHelper));
        }
        if (command.UseShellExecute || command.UserName.Length != 0)
        {
            throw new ArgumentException("The isolated test command must run directly as the current user.", nameof(command));
        }
        if (runtimeParent is not null)
        {
            if (!Path.IsPathFullyQualified(runtimeParent) || !Directory.Exists(runtimeParent))
            {
                throw new ArgumentException("The runtime parent must be an existing absolute directory.", nameof(runtimeParent));
            }
            runtimeParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runtimeParent));
            if (File.GetAttributes(runtimeParent).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ArgumentException("The runtime parent must not be a symbolic link.", nameof(runtimeParent));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var child = CopyCommand(command);
        var workspace = new Workspace(Path.GetFullPath(imageStore), networkHelper, runtimeParent);
        var privateHost = host with
        {
            RunProcess = (startInfo, ct) =>
            {
                workspace.SetEnvironment(startInfo);
                return host.RunProcess(startInfo, ct);
            }
        };
        var verified = false;
        var cleaned = false;
        var childTimedOut = false;
        var exitCode = 1;
        try
        {
            await workspace.WriteConfiguration(cancellationToken).ConfigureAwait(false);
            await workspace.Verify(privateHost, cancellationToken).ConfigureAwait(false);
            verified = true;
            if (await PodmanDnsPreflight.Run(privateHost, quiet: true, output, error, cancellationToken).ConfigureAwait(false) == 0)
            {
                workspace.SetEnvironment(child);
                ProcessOutput result;
                try
                {
                    result = await runner.Run(child, forward: null, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    childTimedOut = true;
                    throw;
                }
                await output.WriteAsync(result.StandardOutput.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteAsync(result.StandardError.AsMemory(), cancellationToken).ConfigureAwait(false);
                exitCode = result.ExitCode;
            }
        }
        finally
        {
            if (verified)
            {
                cleaned = await TryCleanup(workspace, privateHost, output, error, childTimedOut).ConfigureAwait(false);
            }
            if (!cleaned)
            {
                var message = $"run-isolated-podman: retained private engine files at {ControlCharacters.Escape(workspace.Root)}.";
                await error.WriteLineAsync(message.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                if (workspace.RuntimeCreated && Path.Exists(workspace.RuntimeRoot))
                {
                    var runtimeMessage =
                        $"run-isolated-podman: retained private runtime files at {ControlCharacters.Escape(workspace.RuntimeRoot)}.";
                    await error.WriteLineAsync(runtimeMessage.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        return cleaned || exitCode != 0 ? exitCode : 1;
    }

    private static ProcessStartInfo CopyCommand(ProcessStartInfo source)
    {
        var copy = new ProcessStartInfo(source.FileName, source.Arguments)
        {
            WorkingDirectory = source.WorkingDirectory,
            RedirectStandardInput = source.RedirectStandardInput,
            RedirectStandardOutput = source.RedirectStandardOutput,
            RedirectStandardError = source.RedirectStandardError,
            StandardInputEncoding = source.StandardInputEncoding,
            StandardOutputEncoding = source.StandardOutputEncoding,
            StandardErrorEncoding = source.StandardErrorEncoding,
        };
        foreach (var argument in source.ArgumentList)
        {
            copy.ArgumentList.Add(argument);
        }
        copy.Environment.Clear();
        foreach (var (name, value) in source.Environment)
        {
            copy.Environment.Add(name, value);
        }
        return copy;
    }

    private static async Task<bool> TryCleanup(
        Workspace workspace, PodmanDnsHost host, TextWriter output, TextWriter error, bool childTimedOut)
    {
        using var deadline = new CancellationTokenSource(CleanupTimeout);
        try
        {
            await workspace.Verify(host, deadline.Token).ConfigureAwait(false);
            await Query(host, ["pod", "rm", "--all", "--force", "--time", "0"], deadline.Token).ConfigureAwait(false);
            await RemoveContainers(workspace, host, childTimedOut, deadline.Token).ConfigureAwait(false);
            await Query(host, ["network", "prune", "--force"], deadline.Token).ConfigureAwait(false);
            await Query(host, ["volume", "rm", "--all", "--force"], deadline.Token).ConfigureAwait(false);

            var containers = await Query(host, ["ps", "--all", "--quiet"], deadline.Token).ConfigureAwait(false);
            var networks = await Query(host, ["network", "ls", "--format", "{{.Name}}"], deadline.Token).ConfigureAwait(false);
            if (containers.Length != 0 || !string.Equals(networks, DefaultNetwork, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The private engine still has containers or user-defined networks.");
            }
            if (await PodmanDnsPreflight.Run(host, quiet: true, output, error, deadline.Token).ConfigureAwait(false) != 0)
            {
                return false;
            }

            workspace.VerifyFiles();
            await Query(host, ["image", "rm", "--all"], deadline.Token).ConfigureAwait(false);
            workspace.Delete();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            var message = exception is OperationCanceledException ? "The cleanup deadline expired." : exception.Message;
            await error.WriteLineAsync($"run-isolated-podman: cleanup failed. {ControlCharacters.Escape(message)}").ConfigureAwait(false);
            return false;
        }
    }

    private static async Task RemoveContainers(
        Workspace workspace, PodmanDnsHost host, bool childTimedOut, CancellationToken cancellationToken)
    {
        string[] arguments = ["rm", "--all", "--force", "--time", "0"];
        var result = await RunBoundedQuery(host, arguments, cancellationToken).ConfigureAwait(false);
        if (childTimedOut && result.ExitCode != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await workspace.Verify(host, cancellationToken).ConfigureAwait(false);
            workspace.VerifyFiles();
            await Query(host, arguments, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.ExitCode != 0)
        {
            throw QueryFailure(arguments[0]);
        }
    }

    private static async Task<string> Query(PodmanDnsHost host, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await RunBoundedQuery(host, arguments, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.TrimEnd('\r', '\n') : throw QueryFailure(arguments[0]);
    }

    private static async Task<PodmanCommandResult> RunBoundedQuery(
        PodmanDnsHost host, string[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo(host.PodmanExecutable!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--remote=false");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        var result = await host.RunProcess(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.StandardOutput.Length > 1_048_576 || result.StandardError.Length > 1_048_576)
        {
            throw QueryFailure(arguments[0]);
        }

        return result;
    }

    private static InvalidOperationException QueryFailure(string operation) =>
        new($"Local Podman could not complete the '{operation}' operation for the private engine.");

    private sealed class Workspace
    {
        private const string TemporaryDirectoryPrefix = "sharedkernel-podman-";
        private const string ScopeFormat = "{{.Host.ServiceIsRemote}}\n{{.Host.Security.Rootless}}\n{{.Store.GraphRoot}}\n{{.Store.RunRoot}}\n{{.Host.NetworkBackend}}\n{{.Store.ConfigFile}}\n{{.Store.VolumePath}}";
        private static readonly string[] AmbientContainerVariables =
        [
            "CONTAINER_CONNECTION", "CONTAINER_HOST", "CONTAINERS_CONF_OVERRIDE", "DOCKER_CONTEXT", "DOCKER_HOST", "STORAGE_DRIVER", "STORAGE_OPTS"
        ];

        private readonly string _graph;
        private readonly string _run;
        private readonly string _storageConfig;
        private readonly string _engineConfig;
        private readonly string _expectedStorage;
        private readonly string _expectedEngine;
        private readonly string[] _privateDirectories;
        private readonly string _volumes;
        private readonly string? _runtimeParent;

        public Workspace(string imageStore, string networkHelper, string? runtimeParent)
        {
            var storeValue = QuotePath(imageStore);
            _runtimeParent = runtimeParent;
            Root = Directory.CreateTempSubdirectory(TemporaryDirectoryPrefix).FullName;
            RuntimeRoot = runtimeParent is null
                ? Root
                : Path.Combine(runtimeParent, TemporaryDirectoryPrefix + Path.GetRandomFileName());
            _graph = Path.Combine(Root, "graph");
            _run = Path.Combine(RuntimeRoot, "run");
            _storageConfig = Path.Combine(Root, "storage.conf");
            _engineConfig = Path.Combine(Root, "containers.conf");
            var temp = Path.Combine(RuntimeRoot, "libpod");
            var state = Path.Combine(_graph, "libpod");
            _volumes = Path.Combine(_graph, "volumes");
            var networks = Path.Combine(HasSeparateRuntime ? RuntimeRoot : _graph, "networks");
            _privateDirectories = [Root, _graph, _run, temp, state, _volumes, networks];
            _expectedStorage = $"""
                [storage]
                driver = "overlay"
                graphroot = {QuotePath(_graph)}
                rootless_storage_path = {QuotePath(_graph)}
                runroot = {QuotePath(_run)}
                [storage.options]
                additionalimagestores = [{storeValue}]
                """;
            _expectedEngine = $"""
                [engine]
                lock_type = "file"
                tmp_dir = {QuotePath(temp)}
                static_dir = {QuotePath(state)}
                volume_path = {QuotePath(_volumes)}
                [network]
                network_backend = "netavark"
                network_config_dir = {QuotePath(networks)}
                default_rootless_network_cmd = "{networkHelper}"
                """;
        }

        public string Root { get; }

        public string RuntimeRoot { get; }

        public bool HasSeparateRuntime => _runtimeParent is not null;

        public bool RuntimeCreated { get; private set; }

        public async Task WriteConfiguration(CancellationToken ct)
        {
            if (_runtimeParent is { } runtimeParent)
            {
                VerifyDirectory(runtimeParent);
                if (Path.Exists(RuntimeRoot))
                {
                    throw new IOException("The private runtime directory already exists.");
                }
                if (OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(RuntimeRoot);
                }
                else
                {
                    Directory.CreateDirectory(RuntimeRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                VerifyDirectory(RuntimeRoot);
                RuntimeCreated = true;
            }
            foreach (var directory in _privateDirectories)
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(_storageConfig, _expectedStorage, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(_engineConfig, _expectedEngine, ct).ConfigureAwait(false);
        }

        public void SetEnvironment(ProcessStartInfo startInfo)
        {
            ProcessEnvironment.RemoveForgeTokens(startInfo);
            ProcessEnvironment.Remove(startInfo, AmbientContainerVariables);
            startInfo.Environment["CONTAINERS_STORAGE_CONF"] = _storageConfig;
            startInfo.Environment["CONTAINERS_CONF"] = _engineConfig;
            startInfo.Environment["ASPIRE_CONTAINER_RUNTIME"] = "podman";
        }

        public async Task Verify(PodmanDnsHost host, CancellationToken ct)
        {
            VerifyFiles();
            var actual = await Query(host, ["info", "--format", ScopeFormat], ct).ConfigureAwait(false);
            var expected = $"false\ntrue\n{_graph}\n{_run}\nnetavark\n{_storageConfig}\n{_volumes}";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Podman did not report the expected local rootless engine and private paths.");
            }
        }

        public void VerifyFiles()
        {
            if (_runtimeParent is { } runtimeParent)
            {
                VerifyDirectory(runtimeParent);
                VerifyDirectory(RuntimeRoot);
            }
            foreach (var directory in _privateDirectories)
            {
                VerifyDirectory(directory);
            }
            VerifyConfig(_storageConfig, _expectedStorage);
            VerifyConfig(_engineConfig, _expectedEngine);
        }

        public void Delete()
        {
            VerifyDirectory(Root);
            if (_runtimeParent is { } runtimeParent)
            {
                VerifyDirectory(runtimeParent);
                VerifyDirectory(RuntimeRoot);
                Directory.Delete(RuntimeRoot, recursive: true);
            }
            Directory.Delete(Root, recursive: true);
        }

        private static void VerifyDirectory(string directory)
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
            {
                throw new InvalidOperationException("A private engine directory was replaced.");
            }
        }

        private static void VerifyConfig(string path, string expected)
        {
            var file = new FileInfo(path);
            if ((file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0
                || file.Length != System.Text.Encoding.UTF8.GetByteCount(expected)
                || !string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A private engine configuration file changed.");
            }
        }

        private static string QuotePath(string path)
        {
            if (path.Contains('$', StringComparison.Ordinal) || path.Any(char.IsControl))
            {
                throw new ArgumentException("Podman paths cannot contain environment substitutions or control characters.");
            }

            return $"\"{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }
    }
}
