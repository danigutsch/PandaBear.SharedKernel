namespace PandaBear.Mediator.Benchmarks;

/// <summary>Recorded analyzer performance baseline that later runs are compared against.</summary>
/// <param name="SchemaVersion">Version of the baseline file format.</param>
/// <param name="Runtime">Runtime description the baseline was collected on.</param>
/// <param name="OperatingSystem">Operating system description the baseline was collected on.</param>
/// <param name="Architecture">Process architecture the baseline was collected on.</param>
/// <param name="SourceCount">Number of synthetic source files analyzed per measurement.</param>
/// <param name="Samples">Number of timed samples taken per suite.</param>
/// <param name="Iterations">Number of analyzer runs per sample.</param>
/// <param name="MaximumTimeRatio">Largest allowed ratio of measured time to baseline time.</param>
/// <param name="MaximumAllocationRatio">Largest allowed ratio of measured allocations to baseline allocations.</param>
/// <param name="Measurements">Baseline measurement for each analyzer suite.</param>
internal sealed record AnalyzerRegressionBaseline(
    int SchemaVersion,
    string Runtime,
    string OperatingSystem,
    string Architecture,
    int SourceCount,
    int Samples,
    int Iterations,
    double MaximumTimeRatio,
    double MaximumAllocationRatio,
    AnalyzerRegressionMeasurement[] Measurements);
