namespace PandaBear.Mediator.Benchmarks;

internal sealed record AnalyzerRegressionMeasurement(string Suite, double Milliseconds, double CpuMilliseconds, long AllocatedBytes, int Diagnostics);
