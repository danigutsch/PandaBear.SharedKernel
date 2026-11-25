using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using SharedKernel.Processes;

namespace SharedKernel.LoadTesting.K6;

/// <summary>
/// Runs <see href="https://grafana.com/docs/k6/latest/">k6</see> as the load generator of a
/// <see cref="LoadTestRunner"/>: <c>k6 run --no-usage-report --summary-export &lt;run&gt;/k6-summary.json</c> with
/// <c>-e</c> for each variable, the checked arguments, and the script. Local k6 starts with only the variables Windows
/// needs to start a process; a container run uses <c>--rm --pull=never</c>, mounts the scripts read-only and the run
/// directory, and force-removes the container on cancellation.
/// </summary>
public sealed class K6LoadGenerator : ILoadGenerator
{
    /// <summary>The k6 variable that the generator sets so k6 sends no usage report.</summary>
    public const string UsageReportVariable = "K6_NO_USAGE_REPORT";

    private static readonly TimeSpan ContainerRemovalTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner _processRunner;

    /// <summary>Creates the generator from the process runner that starts k6 or the container engine.</summary>
    /// <param name="processRunner">Runs k6 and the container engine, such as <see cref="SystemProcessRunner"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="processRunner"/> is <see langword="null"/>.</exception>
    public K6LoadGenerator(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        _processRunner = processRunner;
    }

    /// <inheritdoc />
    public string Command => "k6";

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> ControlledVariables { get; } = [new(UsageReportVariable, "true")];

    /// <inheritdoc />
    /// <remarks>
    /// Rejects <c>--include-system-env-vars</c>, <c>--insecure-skip-tls-verify</c>, <c>--summary-export</c>, a short
    /// option with an attached value or bundled with another, and <c>-e</c> or <c>--env</c> for a controlled variable;
    /// <c>--http-debug</c> needs <see cref="LoadTestSettings.AllowHttpDebug"/>, and <c>--out</c>, <c>-o</c>,
    /// <c>--log-output</c>, <c>--console-output</c>, and <c>--traces-output</c> need <see cref="LoadTestSettings.AllowRemoteOutput"/>.
    /// </remarks>
    public void ValidateArguments(IReadOnlyList<string> arguments, IReadOnlyCollection<string> controlledVariables, LoadTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(controlledVariables);
        ArgumentNullException.ThrowIfNull(settings);
        K6ArgumentPolicy.Validate(arguments, controlledVariables, settings.AllowHttpDebug, settings.AllowRemoteOutput);
    }

    /// <inheritdoc />
    /// <remarks>
    /// k6 exits 0 when it cannot write the end-of-test summary, so a container run that exits 0 without
    /// <c>k6-summary.json</c> in the run directory throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    public Task<int> Run(LoadGeneratorRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.Container is { } container
            ? RunContainer(run, container, ct)
            : RunToExit(K6StartInfo.Local(run), ct);
    }

    private async Task<int> RunContainer(LoadGeneratorRun run, LoadTestContainer container, CancellationToken ct)
    {
        var name = "sharedkernel-k6-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var startInfo = K6StartInfo.Container(run, container, name);
        int exitCode;
        try
        {
            exitCode = await RunToExit(startInfo, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping the engine client can leave the container running against the target.
            await RemoveContainer(run, name).ConfigureAwait(false);
            throw;
        }

        return exitCode == 0 && !File.Exists(Path.Combine(run.RunDirectory, K6StartInfo.SummaryFile))
            ? throw new InvalidOperationException($"k6 exited 0 but wrote no {K6StartInfo.SummaryFile} in the results directory of the run. The container user must be able to write the results directory.")
            : exitCode;
    }

    private async Task<int> RunToExit(ProcessStartInfo startInfo, CancellationToken ct)
    {
        // A start failure is the InvalidOperationException that IProcessRunner documents. SystemProcessRunner and
        // BoundedProcessRunner put the file name and the operating system's reason in its message; another runner may not.
        var output = await _processRunner.Run(startInfo, null, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return output.ExitCode;
    }

    private async Task RemoveContainer(LoadGeneratorRun run, string name)
    {
        try
        {
            using var removal = new CancellationTokenSource(ContainerRemovalTimeout);
            var output = await _processRunner.Run(K6StartInfo.ContainerRemoval(run, name), null, removal.Token).ConfigureAwait(false);
            if (output.ExitCode == 0)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException or IOException or Win32Exception)
        {
            // The message below names the container to remove.
        }

        await run.Error.WriteLineAsync($"Container {name} may still be running. Remove it with: {Path.GetFileName(run.Executable)} rm --force {name}").ConfigureAwait(false);
    }
}
