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

    private static async Task<CoverageConfiguration> ReadCoverage(string root, CancellationToken ct)
    {
        var configuration = await CiConfigurationFile.Read(root, ct).ConfigureAwait(false);
        return configuration.Coverage ?? throw CiConfigurationFile.Invalid("$.coverage is required by the coverage commands.");
    }
}
