using System.Globalization;

namespace PandaBear.CI;

/// <summary>
/// Records the Release symbols of a build, and verifies the changed-code coverage of Cobertura reports with the coverage
/// policy of <c>.config/sharedkernel-ci.json</c>.
/// </summary>
public sealed class CoverageVerifier
{
    /// <summary>The default symbol evidence path, relative to the root.</summary>
    public const string DefaultSymbolEvidencePath = SymbolEvidence.DefaultPath;

    /// <summary>The default gate evidence path, relative to the root.</summary>
    public const string DefaultGateEvidencePath = CoverageGate.DefaultOutput;

    /// <summary>The file name pattern of a Cobertura report that a directory search finds.</summary>
    public const string ReportFilePattern = CoberturaReport.FilePattern;

    private readonly CiMachine _machine;

    /// <summary>Creates the verifier from the machine that runs Git.</summary>
    /// <param name="machine">Runs Git.</param>
    /// <exception cref="ArgumentNullException"><paramref name="machine"/> is <see langword="null"/>.</exception>
    public CoverageVerifier(CiMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
    }

    /// <summary>Records <c>HEAD</c> and the SHA-256 hash of every Release portable PDB under the configured symbol directories.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="evidencePath">The evidence file to write, relative to the root.</param>
    /// <param name="output">Receives a line that names the evidence.</param>
    /// <param name="ct">Cancels Git.</param>
    /// <exception cref="InvalidOperationException">The configuration has no coverage policy, no symbol file exists, one is reached through a symbolic link, or Git failed.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async Task RecordSymbols(string root, string evidencePath, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(evidencePath);
        ArgumentNullException.ThrowIfNull(output);
        var coverage = await ReadCoverage(root, ct).ConfigureAwait(false);
        await SymbolEvidence.Record(root, coverage, evidencePath, _machine.Run, output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies the symbol evidence, then requires the configured share of the executable lines and conditions added since
    /// <paramref name="baseRevision"/> to be covered, and writes the gate evidence.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="reports">Cobertura reports, or directories searched for <see cref="ReportFilePattern"/>, relative to the root.</param>
    /// <param name="baseRevision">The full Git object ID of the base; the lines added in <c>&lt;base&gt;...HEAD</c> count.</param>
    /// <param name="symbolEvidencePath">The symbol evidence that <see cref="RecordSymbols"/> wrote for <c>HEAD</c>, relative to the root.</param>
    /// <param name="gateEvidencePath">The gate evidence file to write, relative to the root.</param>
    /// <param name="output">Receives the result.</param>
    /// <param name="ct">Cancels Git.</param>
    /// <exception cref="InvalidOperationException">The coverage is below the threshold, the evidence does not match, or Git failed.</exception>
    /// <exception cref="InvalidDataException">A report, the diff, or the evidence is invalid, or changed code has no coverage.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task Verify(
        string root, IReadOnlyList<string> reports, string baseRevision, string symbolEvidencePath, string gateEvidencePath, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(baseRevision);
        ArgumentNullException.ThrowIfNull(symbolEvidencePath);
        ArgumentNullException.ThrowIfNull(gateEvidencePath);
        ArgumentNullException.ThrowIfNull(output);
        return CoverageGate.Verify(root, [.. reports], baseRevision, symbolEvidencePath, gateEvidencePath, _machine.Run, output, ct);
    }

    /// <summary>Reports the changed executable lines and conditions in the Git index, using local Cobertura reports and Release symbols.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="reports">Cobertura files or directories searched for <see cref="ReportFilePattern"/>, relative to the root.</param>
    /// <param name="output">Receives the measured counts.</param>
    /// <param name="ct">Cancels Git and output.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Tracked files differ from the index, the policy is absent, or Git failed.</exception>
    /// <exception cref="InvalidDataException">An unreported included source file lacks matching symbols that show it has no executable code, or the diff, report, or symbols are invalid.</exception>
    /// <exception cref="TimeoutException">Git did not finish in time.</exception>
    public Task MeasureStaged(string root, IReadOnlyList<string> reports, TextWriter output, CancellationToken ct) =>
        MeasureStaged(root, reports, baseRevision: null, output, ct);

    /// <summary>Measures the changed lines and conditions in the index relative to a Git base.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="reports">Cobertura files or directories containing reports, relative to the root.</param>
    /// <param name="baseRevision">A full Git commit or tree object ID, or <see langword="null" /> to compare against <c>HEAD</c>.</param>
    /// <param name="output">Receives the measured counts.</param>
    /// <param name="ct">Cancels Git and output.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The base is invalid, tracked files differ from the index, the policy is absent, or Git failed.</exception>
    public async Task MeasureStaged(string root, IReadOnlyList<string> reports, string? baseRevision, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(output);
        await MeasureStagedCore(root, reports, baseRevision, withEvidence: false, output, ct).ConfigureAwait(false);
    }

    /// <summary>Measures staged coverage from reports captured and hashed by a run of the current staged tree.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="evidence">Evidence files written by a coverage run, relative to the root.</param>
    /// <param name="output">Receives the measured counts.</param>
    /// <param name="ct">Cancels Git, evidence loading, and output.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No evidence file was supplied.</exception>
    /// <exception cref="InvalidOperationException">Tracked files differ from the index, evidence records another tree, the policy is absent, or Git failed.</exception>
    /// <exception cref="InvalidDataException">Evidence or a report is invalid, its recorded hash differs, or included staged code has no coverage or matching symbols.</exception>
    /// <exception cref="TimeoutException">Git did not finish in time.</exception>
    public Task MeasureStagedWithEvidence(string root, IReadOnlyList<string> evidence, TextWriter output, CancellationToken ct) =>
        MeasureStagedWithEvidence(root, evidence, baseRevision: null, output, ct);

    /// <summary>Measures the verified staged tree relative to a Git base using the captured coverage evidence.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="evidence">Evidence files written by a coverage run, relative to the root.</param>
    /// <param name="baseRevision">A full Git commit or tree object ID, or <see langword="null" /> to compare against <c>HEAD</c>.</param>
    /// <param name="output">Receives the measured counts.</param>
    /// <param name="ct">Cancels Git, evidence loading, and output.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">No evidence file was supplied.</exception>
    /// <exception cref="InvalidOperationException">The base is invalid, the evidence names another tree, the policy is absent, or Git failed.</exception>
    public async Task MeasureStagedWithEvidence(
        string root, IReadOnlyList<string> evidence, string? baseRevision, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(output);
        if (evidence.Count == 0)
        {
            throw new ArgumentException("At least one coverage evidence file is required.", nameof(evidence));
        }

        await MeasureStagedCore(root, evidence, baseRevision, withEvidence: true, output, ct).ConfigureAwait(false);
    }

    private async Task MeasureStagedCore(
        string root, IReadOnlyList<string> inputs, string? baseRevision, bool withEvidence, TextWriter output, CancellationToken ct)
    {
        if (baseRevision is not null && !ProjectSelectionPlan.IsObjectId(baseRevision))
        {
            throw new InvalidOperationException("--base requires a full hexadecimal Git object ID.");
        }

        var coverage = await ReadCoverage(root, ct).ConfigureAwait(false);
        Dictionary<string, string>? hashes = null;
        string? tree = null;
        if (withEvidence)
        {
            tree = await CoverageReportEvidence.ReadTree(root, _machine.RunGit, ct).ConfigureAwait(false);
            hashes = await CoverageReportEvidence.Read(root, inputs, tree, ct).ConfigureAwait(false);
        }
        else
        {
            await CoverageReportEvidence.RequireTrackedMatch(root, _machine.RunGit, ct).ConfigureAwait(false);
        }

        var comparisonBase = baseRevision ?? "HEAD";
        string[] revisions = tree is null ? ["--cached", comparisonBase] : [comparisonBase, tree];
        var patch = await _machine.RunGit([.. CoverageGate.DiffArguments, .. revisions, "--"], root, ct).ConfigureAwait(false);
        var changes = ChangedLines.Parse(patch, coverage.Includes);
        IEnumerable<string> files = hashes is null ? CoberturaReport.Find(root, inputs) : hashes.Keys;
        var report = CoberturaReport.Read(root, files, coverage.Includes, hashes);
        var missing = changes.Keys.Where(path => !report.ContainsKey(path)).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
        {
            var symbols = SymbolEvidence.FindReleaseFiles(root, coverage);
            var unreported = missing.Where(path => !PortableSymbols.HasOnlyNonExecutableCode(root, path, symbols)).ToArray();
            if (unreported.Length > 0)
            {
                throw new InvalidDataException("Included staged source files in no coverage report: " + string.Join(", ", unreported) + ".");
            }
        }

        long covered = 0;
        long total = 0;
        foreach (var (path, changedLines) in changes.OrderBy(static change => change.Key, StringComparer.Ordinal))
        {
            var counts = report.TryGetValue(path, out var fileCoverage)
                ? LineCoverage.CountChanged(changedLines, fileCoverage)
                : default;
            covered += counts.HitLines + counts.HitConditions;
            total += counts.Lines + counts.Conditions;
            var summary = string.Create(
                CultureInfo.InvariantCulture,
                $"{path} {counts.HitLines}/{counts.Lines} executable lines, {counts.HitConditions}/{counts.Conditions} conditions.");
            await output.WriteLineAsync(summary.AsMemory(), ct).ConfigureAwait(false);
        }

        await output.WriteLineAsync(
            string.Create(CultureInfo.InvariantCulture, $"Staged coverage measured {covered}/{total} changed executable lines and conditions.").AsMemory(),
            ct).ConfigureAwait(false);
    }

    private static async Task<CoverageConfiguration> ReadCoverage(string root, CancellationToken ct)
    {
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        return configuration.Coverage ?? throw CiConfigurationFile.Invalid("$.coverage is required by the coverage commands.");
    }
}
