namespace PandaBear.Mediator.Benchmarks;

/// <summary>Time, CPU time, allocation, and diagnostic count measured for one analyzer suite.</summary>
/// <param name="Suite">Name of the analyzer suite.</param>
/// <param name="Milliseconds">Wall-clock time in milliseconds.</param>
/// <param name="CpuMilliseconds">CPU time in milliseconds.</param>
/// <param name="AllocatedBytes">Bytes allocated during the run.</param>
/// <param name="Diagnostics">Number of diagnostics the suite reported.</param>
internal sealed record AnalyzerRegressionMeasurement(string Suite, double Milliseconds, double CpuMilliseconds, long AllocatedBytes, int Diagnostics);
