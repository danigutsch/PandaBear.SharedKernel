namespace PandaBear.CI;

/// <summary>
/// Checks the changed projects without a Unix domain socket: whitespace in folder mode, a build with node reuse off, and
/// the built test executables run directly, then reports what could not run.
/// </summary>
public sealed class LocalCheckRunner
{
    private readonly CiMachine _machine;

    /// <summary>Creates the runner from the machine that runs Git, dotnet, and the test executables.</summary>
    /// <param name="machine">Runs the child processes and times the lanes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public LocalCheckRunner(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Selects the changed projects, runs the lanes, and writes the report.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selectionDirectory">The absolute directory for the plan, the solution filter, and the lane logs.</param>
    /// <param name="options">The base, configuration, strictness, test arguments, and timeout.</param>
    /// <param name="output">Receives the report.</param>
    /// <param name="error">Receives warnings of the selection.</param>
    /// <param name="ct">Cancels the check; the running child process tree is terminated first.</param>
    /// <exception cref="InvalidOperationException">
    /// The root is not in a Git work tree, the base is not a commit, the change selects no project, a lane failed, no lane
    /// passed, or <see cref="LocalCheckOptions.Strict"/> is set and a lane could not run.
    /// </exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task Run(string root, string selectionDirectory, LocalCheckOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(selectionDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return LocalCheck.Run(root, selectionDirectory, options, _machine.Run, _machine.RunConfigured, output, error, ct, _machine.TimeProvider);
    }
}
