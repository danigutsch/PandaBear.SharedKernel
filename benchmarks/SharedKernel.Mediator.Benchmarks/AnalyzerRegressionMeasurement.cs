namespace SharedKernel.Mediator.Benchmarks;

/// <summary>Time, CPU time, allocation, and diagnostic count measured for one analyzer suite.</summary>
/// <param name="Suite">Name of the analyzer suite.</param>
/// <param name="Milliseconds">Wall-clock time in milliseconds.</param>
/// <param name="CpuMilliseconds">CPU time in milliseconds.</param>
/// <param name="AnalyzerMilliseconds">Execution time reported by the Roslyn analyzer driver in milliseconds.</param>
/// <param name="AllocatedBytes">Bytes allocated during the run.</param>
/// <param name="Diagnostics">Number of diagnostics the suite reported.</param>
/// <param name="CompilerComparison">Elapsed-time comparison to a fresh compilation of the same corpus without analyzers.</param>
internal sealed record AnalyzerRegressionMeasurement(
    string Suite,
    double Milliseconds,
    double CpuMilliseconds,
    double AnalyzerMilliseconds,
    long AllocatedBytes,
    int Diagnostics,
    AnalyzerCompilerComparison CompilerComparison);
