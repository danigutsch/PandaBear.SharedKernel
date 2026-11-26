namespace SharedKernel.Mediator.Benchmarks;

/// <summary>Recorded analyzer performance baseline that later runs are compared against.</summary>
/// <param name="SchemaVersion">Version of the baseline file format.</param>
/// <param name="Runtime">Runtime description the baseline was collected on.</param>
/// <param name="OperatingSystem">Operating system description the baseline was collected on.</param>
/// <param name="Architecture">Process architecture the baseline was collected on.</param>
/// <param name="DotnetTieredCompilation">Configured .NET runtime tiering value.</param>
/// <param name="ComPlusTieredCompilation">Configured compatibility tiering value.</param>
/// <param name="SourceCounts">Numbers of declarations or operations in the measured workloads.</param>
/// <param name="Samples">Number of timed samples taken per suite.</param>
/// <param name="Iterations">Number of analyzer runs per sample.</param>
/// <param name="MaximumCompilerOverheadRatio">Largest allowed ratio of measured paired overhead to baseline overhead.</param>
/// <param name="MaximumAllocationRatio">Largest allowed ratio of measured allocations to baseline allocations.</param>
/// <param name="Measurements">Baseline measurement for each analyzer suite.</param>
internal sealed record AnalyzerRegressionBaseline(
    int SchemaVersion,
    string Runtime,
    string OperatingSystem,
    string Architecture,
    string? DotnetTieredCompilation,
    string? ComPlusTieredCompilation,
    int[] SourceCounts,
    int Samples,
    int Iterations,
    double MaximumCompilerOverheadRatio,
    double MaximumAllocationRatio,
    AnalyzerRegressionMeasurement[] Measurements);
