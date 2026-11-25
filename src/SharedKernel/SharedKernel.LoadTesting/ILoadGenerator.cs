namespace SharedKernel.LoadTesting;

/// <summary>
/// A program that generates load from a script, such as k6. <see cref="LoadTestRunner"/> reads the configuration,
/// resolves the target, and prepares the run directory; the generator checks its own arguments and runs.
/// </summary>
public interface ILoadGenerator
{
    /// <summary>Gets the command name of a local generator, resolved from the absolute directories of <c>PATH</c>.</summary>
    string Command { get; }

    /// <summary>
    /// Gets the variables, in order, that the generator always passes to the script after the base URL. Scenarios cannot
    /// name them, and the arguments cannot set them.
    /// </summary>
    IReadOnlyList<KeyValuePair<string, string>> ControlledVariables { get; }

    /// <summary>Rejects arguments that would widen what a run can read, send, or write.</summary>
    /// <param name="arguments">The arguments, in order.</param>
    /// <param name="controlledVariables">The variables that the run sets, which the arguments cannot set.</param>
    /// <param name="settings">The settings of the run, which say what it allows.</param>
    /// <exception cref="InvalidOperationException">An argument is not allowed.</exception>
    void ValidateArguments(IReadOnlyList<string> arguments, IReadOnlyCollection<string> controlledVariables, LoadTestSettings settings);

    /// <summary>Runs the generator and returns its exit code.</summary>
    /// <param name="run">The prepared run.</param>
    /// <param name="ct">Cancels the run and stops the generator and its container.</param>
    /// <exception cref="InvalidOperationException">The generator could not start or did not write its results.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    Task<int> Run(LoadGeneratorRun run, CancellationToken ct);
}
