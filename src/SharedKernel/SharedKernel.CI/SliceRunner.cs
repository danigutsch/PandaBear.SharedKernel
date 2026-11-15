namespace PandaBear.CI;

/// <summary>
/// Restores, builds, and tests the selected test projects of the validation plan through one solution filter, after the
/// slices' OpenAPI targets and Playwright browsers, with the slices' services running during the tests, and records the
/// phases, a manifest, and diagnostics whether or not the run succeeds.
/// </summary>
public sealed class SliceRunner
{
    private readonly CiMachine _machine;

    /// <summary>Creates the runner from the machine that runs Git, dotnet, Playwright, and the services.</summary>
    /// <param name="machine">Runs the child processes and starts the services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public SliceRunner(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Runs the selected slices of the plan in <paramref name="selectionDirectory"/>.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selectionDirectory">The absolute directory that holds the plan; the run writes its evidence below it.</param>
    /// <param name="options">The slices and how they run.</param>
    /// <param name="summary">The step summary that receives the phases, or <see langword="null"/>.</param>
    /// <param name="output">Receives a line when nothing is selected.</param>
    /// <param name="error">Receives the progress output of the restore, build, generation, installation, and tests.</param>
    /// <param name="ct">Cancels the run; the services are stopped and the evidence is written first.</param>
    /// <exception cref="InvalidOperationException">A phase failed, no Cobertura report exists for a selected test project, or a service failed to stop.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task Run(string root, string selectionDirectory, SliceRunOptions options, IStepSummary? summary, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(selectionDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return SliceRun.Run(
            root,
            selectionDirectory,
            options,
            summary,
            _machine.Run,
            _machine.RunConfigured,
            output,
            error,
            ct,
            _machine.TimeProvider,
            _machine.StartService,
            (fileName, arguments, directory, token) => _machine.RunStreaming(fileName, arguments, directory, error, token));
    }
}
