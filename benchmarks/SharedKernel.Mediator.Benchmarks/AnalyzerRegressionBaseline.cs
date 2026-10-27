namespace PandaBear.Mediator.Benchmarks;

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
