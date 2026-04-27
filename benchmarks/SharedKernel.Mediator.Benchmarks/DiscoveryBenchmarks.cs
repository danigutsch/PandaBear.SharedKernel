using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures discovery-report generation across the main M03 scale points.
/// </summary>
[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private string baselineSource = string.Empty;
    private CSharpCompilation baselineCompilation = null!;
    private CSharpCompilation editedCompilation = null!;
    private GeneratorDriver baselineDriver = null!;

    /// <summary>
    /// Gets or sets the number of generated request/handler/pipeline triplets in the test source.
    /// </summary>
    [Params(10, 100, 1000, 5000)]
    public int RequestCount { get; set; }

    /// <summary>
    /// Gets the generated source size for the baseline input.
    /// </summary>
    public int BaselineGeneratedSourceLength { get; private set; }

    /// <summary>
    /// Gets the generated source size for the edited input.
    /// </summary>
    public int EditedGeneratedSourceLength { get; private set; }

    /// <summary>
    /// Prepares the baseline and edited compilations used by the benchmark scenarios.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        baselineSource = DiscoveryBenchmarkSourceFactory.CreateSource(RequestCount);
        baselineCompilation = BenchmarkCompilationFactory.CreateCompilation(baselineSource);
        editedCompilation = BenchmarkCompilationFactory.CreateCompilation(
            DiscoveryBenchmarkSourceFactory.CreateSource(RequestCount, editFirstHandler: true));
        baselineDriver = BenchmarkCompilationFactory.CreateGeneratorDriver().RunGenerators(baselineCompilation);
        BaselineGeneratedSourceLength = BenchmarkCompilationFactory.GetGeneratedSourceLength(baselineCompilation);
        EditedGeneratedSourceLength = BenchmarkCompilationFactory.GetGeneratedSourceLength(editedCompilation);
    }

    /// <summary>
    /// Measures the first generator run against a fresh compilation.
    /// </summary>
    /// <returns>The size of the generated discovery report.</returns>
    [Benchmark(Baseline = true, Description = "Clean build")]
    public int CleanBuild()
    {
        return BenchmarkCompilationFactory.GetGeneratedSourceLength(
            BenchmarkCompilationFactory.CreateCompilation(baselineSource));
    }

    /// <summary>
    /// Measures a repeat run against an unchanged compilation and driver graph.
    /// </summary>
    /// <returns>The size of the generated discovery report.</returns>
    [Benchmark(Description = "No-op rebuild")]
    public int NoOpRebuild()
    {
        return BenchmarkCompilationFactory.GetGeneratedSourceLength(baselineCompilation, baselineDriver);
    }

    /// <summary>
    /// Measures a repeat run after a single handler body change.
    /// </summary>
    /// <returns>The size of the generated discovery report.</returns>
    [Benchmark(Description = "One-handler edit rebuild")]
    public int OneHandlerEditRebuild()
    {
        return BenchmarkCompilationFactory.GetGeneratedSourceLength(editedCompilation, baselineDriver);
    }
}
