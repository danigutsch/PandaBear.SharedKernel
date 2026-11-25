using System.Globalization;
using SharedKernel.IO;
using SharedKernel.Processes;

namespace SharedKernel.LoadTesting;

/// <summary>
/// Runs a configured load-test scenario: reads and validates the configuration, checks the generator arguments and the
/// target, creates a directory of its own for the run's results, starts and waits for the scenario's host, runs the
/// <see cref="ILoadGenerator"/>, and stops the host.
/// </summary>
public sealed class LoadTestRunner
{
    private readonly ILoadGenerator _generator;
    private readonly IExecutableResolver _executableResolver;
    private readonly HttpMessageHandler _healthCheckHandler;

    /// <summary>Creates the runner from the load generator and the side effects it uses.</summary>
    /// <param name="generator">The load generator, such as the k6 one.</param>
    /// <param name="executableResolver">Finds the generator, the container engine, and the host command, such as <see cref="PathExecutableResolver.Default"/>.</param>
    /// <param name="healthCheckHandler">
    /// Sends the health probes of a scenario's host. The caller owns it; to reach only the host, it should use no proxy
    /// and follow no redirect, such as a <see cref="SocketsHttpHandler"/> with <c>UseProxy</c> and <c>AllowAutoRedirect</c> off.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public LoadTestRunner(ILoadGenerator generator, IExecutableResolver executableResolver, HttpMessageHandler healthCheckHandler)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(executableResolver);
        ArgumentNullException.ThrowIfNull(healthCheckHandler);
        _generator = generator;
        _executableResolver = executableResolver;
        _healthCheckHandler = healthCheckHandler;
    }

    /// <summary>Validates the run, starts the host when the scenario has one, and runs the load generator.</summary>
    /// <param name="settings">The root, configuration file, scenario, and what the run allows.</param>
    /// <param name="error">Receives the results directory and the host address.</param>
    /// <param name="ct">Cancels the run and stops the generator, its container, and the host.</param>
    /// <returns>The exit code of the load generator.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> or <paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The configuration, an argument, the image, or the target is invalid, an executable or the script is missing, the
    /// host exited, timed out, or reported an invalid address, or a process could not start.
    /// </exception>
    /// <exception cref="IOException">The results directory could not be created.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public async Task<int> Run(LoadTestSettings settings, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(error);

        var controlled = _generator.ControlledVariables;
        var configurationFile = new LoadTestConfigurationFile(settings.ConfigurationPath, [.. controlled.Select(static variable => variable.Key)]);
        var configuration = await configurationFile.Read(settings.Root, ct).ConfigureAwait(false);
        var scenario = configuration.Scenarios.FirstOrDefault(scenario => scenario.Name == settings.Scenario)
            ?? throw new InvalidOperationException(
                $"{configurationFile.RelativePath} defines no scenario {settings.Scenario}. Scenarios: {string.Join(", ", configuration.Scenarios.Select(static scenario => scenario.Name))}.");
        var environment = scenario.Environment ?? [];
        _generator.ValidateArguments(
            settings.GeneratorArguments,
            [scenario.BaseUrlVariable, .. controlled.Select(static variable => variable.Key), .. environment],
            settings);

        LoadTestContainer? container = null;
        if (settings.Container)
        {
            var configured = configuration.Container
                ?? throw configurationFile.Invalid("$.container must name the engine and image that --container uses.");
            if (settings.Image is { } image && !LoadTestContainer.IsDigestPinned(image))
            {
                throw new InvalidOperationException("--image requires an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.");
            }

            container = new LoadTestContainer(configured.Engine, settings.Image ?? configured.Image);
        }

        var runner = Resolve(container?.Engine ?? _generator.Command);
        var scriptPath = configuration.ScriptDirectory + "/" + scenario.Script;
        var script = ResolveUnderRoot(settings.Root, scriptPath);
        if (!File.Exists(script))
        {
            throw new InvalidOperationException($"The script {scriptPath} of scenario {scenario.Name} does not exist.");
        }

        Uri? target = null;
        string? hostExecutable = null;
        if (scenario.Host is { } hostConfiguration)
        {
            hostExecutable = Resolve(hostConfiguration.Command);
        }
        else
        {
            var value = settings.Environment.GetValueOrDefault(scenario.BaseUrlVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{scenario.BaseUrlVariable} is required.");
            }

            target = LoadTestTarget.Parse(value, scenario.BaseUrlVariable, settings.AllowExternalTarget);
        }

        var runName = string.Create(
            CultureInfo.InvariantCulture,
            $"{scenario.Name}-{TimeProvider.System.GetUtcNow():yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]}");
        var relativeRunDirectory = configuration.ResultsDirectory + "/" + runName;
        string runDirectory;
        try
        {
            runDirectory = RepositoryPath.Parse(relativeRunDirectory).ResolveUnder(settings.Root);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{configuration.ResultsDirectory}: {exception.Message}", exception);
        }

        Directory.CreateDirectory(runDirectory);
        if (container is not null && !OperatingSystem.IsWindows())
        {
            // The generator image can run as an unprivileged user that is not the host user. Only the run directory that
            // this call created is shared; the sticky bit keeps that user from deleting or replacing files it does not own.
            File.SetUnixFileMode(
                runDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                | UnixFileMode.StickyBit);
        }

        await error.WriteLineAsync(("Results: " + relativeRunDirectory).AsMemory(), CancellationToken.None).ConfigureAwait(false);

        ManagedHost? host = null;
        try
        {
            if (hostExecutable is not null)
            {
                host = ManagedHost.Start(hostExecutable, scenario.Name, scenario.Host!, settings.Root, runDirectory, relativeRunDirectory, container is not null);
                target = await host.WaitUntilReady(settings.AllowExternalTarget, _healthCheckHandler, ct).ConfigureAwait(false);
                await error.WriteLineAsync(("Host ready: " + LoadTestTarget.Format(target)).AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }

            var variables = new List<KeyValuePair<string, string>>
            {
                new(scenario.BaseUrlVariable, container is null ? LoadTestTarget.Format(target!) : LoadTestTarget.ForContainer(target!)),
            };
            variables.AddRange(controlled);
            foreach (var name in environment)
            {
                if (settings.Environment.GetValueOrDefault(name) is { } value && !string.IsNullOrWhiteSpace(value))
                {
                    variables.Add(new(name, value));
                }
            }

            return await _generator.Run(
                new LoadGeneratorRun
                {
                    Executable = runner,
                    Root = settings.Root,
                    ScriptDirectory = ResolveUnderRoot(settings.Root, configuration.ScriptDirectory),
                    Script = scenario.Script,
                    ScriptPath = script,
                    RunDirectory = runDirectory,
                    Variables = variables,
                    Arguments = settings.GeneratorArguments,
                    Container = container,
                    Environment = settings.Environment,
                    Error = error,
                },
                ct).ConfigureAwait(false);
        }
        finally
        {
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        try
        {
            return RepositoryPath.Parse(relativePath).ResolveUnder(root);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{relativePath}: {exception.Message}", exception);
        }
    }

    private string Resolve(string command) =>
        _executableResolver.Find(command) ?? throw new InvalidOperationException($"{command} was not found in an absolute PATH directory.");
}
