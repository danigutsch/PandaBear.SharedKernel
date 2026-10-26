using System.Diagnostics;
using System.Globalization;
using PandaBear.Tooling;

namespace PandaBear.K6.Tool;

/// <summary>Runs a configured k6 scenario.</summary>
internal static class K6Run
{
    private const string ContainerScripts = "/k6";

    private const string ContainerResults = "/results";

    private const string SummaryFile = "k6-summary.json";

    private static readonly TimeSpan ContainerRemovalTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The variables that local k6 keeps from the environment, which Windows needs to start a process.</summary>
    private static readonly string[] PlatformVariables = ["PATHEXT", "SystemRoot", "WINDIR"];

    /// <summary>Validates the run, starts the host when the scenario has one, and runs k6.</summary>
    /// <param name="options">The parsed options.</param>
    /// <param name="getEnvironmentVariable">Reads the target and the scenario environment.</param>
    /// <param name="resolveExecutable">Resolves k6, the container engine, and the host command.</param>
    /// <param name="error">Receives the results directory and the host address.</param>
    /// <param name="ct">Cancels the run and stops k6, its container, and the host.</param>
    /// <returns>The k6 exit code.</returns>
    /// <exception cref="InvalidOperationException">The configuration, an argument, or the target is invalid, or a process could not start.</exception>
    public static async Task<int> Run(
        K6RunOptions options,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string?> resolveExecutable,
        TextWriter error,
        CancellationToken ct)
    {
        var configuration = await K6ConfigurationFile.Read(options.Root, ct).ConfigureAwait(false);
        var scenario = configuration.Scenarios.FirstOrDefault(scenario => scenario.Name == options.Scenario)
            ?? throw new InvalidOperationException(
                $"{K6ConfigurationFile.RelativePath} defines no scenario {options.Scenario}. Scenarios: {string.Join(", ", configuration.Scenarios.Select(static scenario => scenario.Name))}.");
        var environment = scenario.Environment ?? [];
        K6ArgumentPolicy.Validate(
            options.K6Arguments,
            [scenario.BaseUrlVariable, K6ConfigurationFile.UsageReportVariable, .. environment],
            options.AllowHttpDebug,
            options.AllowRemoteOutput);

        K6ContainerConfiguration? container = null;
        if (options.Container)
        {
            container = configuration.Container
                ?? throw K6ConfigurationFile.Invalid("$.container must name the engine and image that --container uses.");
        }

        var runner = Resolve(resolveExecutable, container?.Engine ?? "k6");
        var scriptPath = configuration.ScriptDirectory + "/" + scenario.Script;
        var script = ResolveUnderRoot(options.Root, scriptPath);
        if (!File.Exists(script))
        {
            throw new InvalidOperationException($"The script {scriptPath} of scenario {scenario.Name} does not exist.");
        }

        Uri? target = null;
        string? hostExecutable = null;
        if (scenario.Host is { } hostConfiguration)
        {
            hostExecutable = Resolve(resolveExecutable, hostConfiguration.Command);
        }
        else
        {
            var value = getEnvironmentVariable(scenario.BaseUrlVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{scenario.BaseUrlVariable} is required.");
            }

            target = K6Target.Parse(value, scenario.BaseUrlVariable, options.AllowExternalTarget);
        }

        var runName = string.Create(
            CultureInfo.InvariantCulture,
            $"{scenario.Name}-{TimeProvider.System.GetUtcNow():yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]}");
        var relativeRunDirectory = configuration.ResultsDirectory + "/" + runName;
        string runDirectory;
        try
        {
            runDirectory = RepositoryPath.Resolve(options.Root, relativeRunDirectory);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{configuration.ResultsDirectory}: {exception.Message}", exception);
        }

        Directory.CreateDirectory(runDirectory);
        if (container is not null && !OperatingSystem.IsWindows())
        {
            // The k6 image runs as an unprivileged user that is not the host user. Only the run directory that this
            // call created is shared; the sticky bit keeps that user from deleting or replacing files it does not own.
            File.SetUnixFileMode(
                runDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                | UnixFileMode.StickyBit);
        }

        await error.WriteLineAsync("Results: " + relativeRunDirectory).ConfigureAwait(false);

        ManagedHost? host = null;
        try
        {
            if (hostExecutable is not null)
            {
                host = ManagedHost.Start(hostExecutable, scenario.Name, scenario.Host!, options.Root, runDirectory, relativeRunDirectory, container is not null);
                target = await host.WaitUntilReady(options.AllowExternalTarget, ct).ConfigureAwait(false);
                await error.WriteLineAsync("Host ready: " + K6Target.Format(target)).ConfigureAwait(false);
            }

            var variables = new List<string>
            {
                scenario.BaseUrlVariable + "=" + (container is null ? K6Target.Format(target!) : K6Target.ForContainer(target!)),
                K6ConfigurationFile.UsageReportVariable + "=true",
            };
            foreach (var name in environment)
            {
                if (getEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value))
                {
                    variables.Add(name + "=" + value);
                }
            }

            return container is null
                ? await RunLocal(runner, options, script, Path.Combine(runDirectory, SummaryFile), variables, getEnvironmentVariable, ct).ConfigureAwait(false)
                : await RunContainer(runner, container, options, ResolveUnderRoot(options.Root, configuration.ScriptDirectory), runDirectory, scenario.Script, variables, error, ct).ConfigureAwait(false);
        }
        finally
        {
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static Task<int> RunLocal(
        string k6,
        K6RunOptions options,
        string script,
        string summary,
        List<string> variables,
        Func<string, string?> getEnvironmentVariable,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(k6) { UseShellExecute = false, WorkingDirectory = options.Root };

        // k6 receives only the -e values, never the caller's environment and its credentials.
        startInfo.Environment.Clear();
        foreach (var name in PlatformVariables)
        {
            if (getEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value))
            {
                startInfo.Environment[name] = value;
            }
        }

        AddK6Arguments(startInfo, summary, variables, options.K6Arguments, script);
        return ChildProcess.Run(startInfo, ct);
    }

    private static async Task<int> RunContainer(
        string engine,
        K6ContainerConfiguration container,
        K6RunOptions options,
        string scripts,
        string runDirectory,
        string script,
        List<string> variables,
        TextWriter error,
        CancellationToken ct)
    {
        var name = "sharedkernel-k6-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var startInfo = new ProcessStartInfo(engine) { UseShellExecute = false, WorkingDirectory = options.Root };
        ChildProcess.RemoveTokens(startInfo);
        foreach (var argument in new[] { "run", "--rm", "--pull=never", "--name", name })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Podman adds the host alias itself; Docker maps it to the host gateway on request.
        if (container.Engine == "docker")
        {
            startInfo.ArgumentList.Add("--add-host");
            startInfo.ArgumentList.Add(K6Target.ContainerHostAlias + ":host-gateway");
        }

        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add("type=bind,source=" + MountSource(scripts) + ",target=" + ContainerScripts + ",readonly");
        startInfo.ArgumentList.Add("--mount");
        startInfo.ArgumentList.Add("type=bind,source=" + MountSource(runDirectory) + ",target=" + ContainerResults);
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add(ContainerScripts);
        startInfo.ArgumentList.Add(options.Image ?? container.Image);
        AddK6Arguments(startInfo, ContainerResults + "/" + SummaryFile, variables, options.K6Arguments, ContainerScripts + "/" + script);
        int exitCode;
        try
        {
            exitCode = await ChildProcess.Run(startInfo, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping the engine client can leave the container running against the target.
            await RemoveContainer(engine, name, options.Root, error).ConfigureAwait(false);
            throw;
        }

        // k6 exits 0 when it cannot write the end-of-test summary, so a missing file is a failed run.
        return exitCode == 0 && !File.Exists(Path.Combine(runDirectory, SummaryFile))
            ? throw new InvalidOperationException($"k6 exited 0 but wrote no {SummaryFile} in the results directory of the run. The container user must be able to write the results directory.")
            : exitCode;
    }

    private static async Task RemoveContainer(string engine, string name, string root, TextWriter error)
    {
        var startInfo = new ProcessStartInfo(engine)
        {
            UseShellExecute = false,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        ChildProcess.RemoveTokens(startInfo);
        foreach (var argument in new[] { "rm", "--force", name })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var removal = new CancellationTokenSource(ContainerRemovalTimeout);
            using var process = ChildProcess.Start(startInfo);
            var drain = Task.WhenAll(process.StandardOutput.ReadToEndAsync(removal.Token), process.StandardError.ReadToEndAsync(removal.Token));
            await process.WaitForExitAsync(removal.Token).ConfigureAwait(false);
            await drain.ConfigureAwait(false);
            if (process.ExitCode == 0)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException or IOException)
        {
            // The message below names the container to remove.
        }

        await error.WriteLineAsync($"Container {name} may still be running. Remove it with: {Path.GetFileName(engine)} rm --force {name}").ConfigureAwait(false);
    }

    private static void AddK6Arguments(ProcessStartInfo startInfo, string summary, List<string> variables, string[] k6Arguments, string script)
    {
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-usage-report");
        startInfo.ArgumentList.Add("--summary-export");
        startInfo.ArgumentList.Add(summary);
        foreach (var variable in variables)
        {
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(variable);
        }

        foreach (var argument in k6Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(script);
    }

    // A mount is a comma-separated list of key=value fields, so a comma or quote in a path would add a field.
    private static string MountSource(string path) =>
        path.AsSpan().IndexOfAny(',', '"') < 0
            ? path
            : throw new InvalidOperationException($"Container mounts cannot name {path}, because it contains ',' or '\"'.");

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        try
        {
            return RepositoryPath.Resolve(root, relativePath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{relativePath}: {exception.Message}", exception);
        }
    }

    private static string Resolve(Func<string, string?> resolveExecutable, string command) =>
        resolveExecutable(command) ?? throw new InvalidOperationException($"{command} was not found in an absolute PATH directory.");
}
