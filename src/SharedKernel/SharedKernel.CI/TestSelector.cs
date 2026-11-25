namespace SharedKernel.CI;

/// <summary>
/// Selects the test projects that a change affects since a base revision, as <c>.config/sharedkernel-ci.json</c>
/// configures, and writes the validation plan and its solution filters.
/// </summary>
public sealed class TestSelector
{
    private readonly CiMachine _machine;

    /// <summary>Creates the selector from the machine that runs Git.</summary>
    /// <param name="machine">Runs Git and times its commands.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public TestSelector(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>
    /// Compares <c>HEAD</c> with <paramref name="baseRevision"/>, writes <c>selection.json</c> and the solution filters to
    /// <paramref name="selectionDirectory"/>, sets <c>build_required</c>, <c>documentation_only</c>, <c>matrix</c>, and
    /// <c>&lt;slice&gt;_required</c> in <paramref name="outputs"/> when given, and writes why each path selected what it did
    /// to <paramref name="output"/>. Without a base, an invalid one, or one that is not an ancestor of <c>HEAD</c>, every
    /// test project is selected.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selectionDirectory">The absolute directory that receives the plan.</param>
    /// <param name="baseRevision">The full Git object ID of the base, or <see langword="null"/>.</param>
    /// <param name="outputs">The run outputs of the forge, or <see langword="null"/>.</param>
    /// <param name="output">Receives the selection reasons.</param>
    /// <param name="error">Receives warnings.</param>
    /// <param name="ct">Cancels Git.</param>
    /// <returns>The plan that was written.</returns>
    /// <exception cref="InvalidOperationException">The configuration or the solution is not valid.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<SelectionPlan> Select(
        string root, string selectionDirectory, string? baseRevision, IRunOutputs? outputs, TextWriter output, TextWriter error, CancellationToken ct) =>
        Select(root, selectionDirectory, baseRevision, candidateExcludedSource: null, outputs, output, error, ct);

    /// <summary>Writes the normal validation plan and optionally reports package candidate applicability.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="selectionDirectory">The absolute directory that receives the plan.</param>
    /// <param name="baseRevision">The full Git object ID of the base, or <see langword="null"/>.</param>
    /// <param name="candidateExcludedSource">
    /// A verified package source exclusion, or <see langword="null"/> to retain the existing forge outputs. The supported value is
    /// <c>benchmarks/**/*.cs</c>. The additional <c>candidate_required</c> output is false only for verified existing benchmark source edits.
    /// </param>
    /// <param name="outputs">The run outputs of the forge, or <see langword="null"/>.</param>
    /// <param name="output">Receives the selection reasons.</param>
    /// <param name="error">Receives warnings.</param>
    /// <param name="ct">Cancels Git.</param>
    /// <returns>The plan that was written.</returns>
    /// <exception cref="InvalidOperationException">The configuration or solution is invalid, or a slice already uses the requested candidate output.</exception>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public Task<SelectionPlan> Select(
        string root, string selectionDirectory, string? baseRevision, string? candidateExcludedSource,
        IRunOutputs? outputs, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(selectionDirectory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return ProjectSelectionPlan.Write(root, selectionDirectory, baseRevision, outputs, _machine.Run, output, error, ct, _machine.TimeProvider,
            candidateExcludedSource: candidateExcludedSource);
    }
}
