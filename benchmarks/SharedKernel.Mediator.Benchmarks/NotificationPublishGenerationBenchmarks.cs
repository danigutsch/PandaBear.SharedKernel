using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures notification publish generation overhead for explicitly ordered versus unordered handlers.
/// </summary>
[Config(typeof(NotificationPublishGenerationBenchmarkConfig))]
[MemoryDiagnoser]
public class NotificationPublishGenerationBenchmarks
{
    private CSharpCompilation orderedCompilation = null!;
    private CSharpCompilation unorderedCompilation = null!;

    /// <summary>
    /// Gets or sets the number of notification handlers under test.
    /// </summary>
    [Params(1, 3, 10, 50)]
    public int HandlerCount { get; set; }

    /// <summary>
    /// Prepares the ordered and unordered generator inputs used by the benchmark scenarios.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        orderedCompilation = BenchmarkCompilationFactory.CreateCompilation(
            NotificationPublishBenchmarkSourceFactory.CreateGenerationSource(
                HandlerCount,
                NotificationPublishBenchmarkSourceFactory.OrderedHandlers),
            $"SharedKernel.Mediator.Benchmarks.NotificationPublish.Generation.Ordered.{HandlerCount}");
        unorderedCompilation = BenchmarkCompilationFactory.CreateCompilation(
            NotificationPublishBenchmarkSourceFactory.CreateGenerationSource(
                HandlerCount,
                NotificationPublishBenchmarkSourceFactory.UnorderedHandlers),
            $"SharedKernel.Mediator.Benchmarks.NotificationPublish.Generation.Unordered.{HandlerCount}");
    }

    /// <summary>
    /// Measures generator output for explicitly ordered handlers.
    /// </summary>
    /// <returns>The total generated source length.</returns>
    [Benchmark(Baseline = true, Description = "Ordered generation overhead")]
    public int OrderedGenerationOverhead()
    {
        return BenchmarkCompilationFactory.GetTotalGeneratedSourceLength(orderedCompilation);
    }

    /// <summary>
    /// Measures generator output for unordered handlers.
    /// </summary>
    /// <returns>The total generated source length.</returns>
    [Benchmark(Description = "Unordered generation overhead")]
    public int UnorderedGenerationOverhead()
    {
        return BenchmarkCompilationFactory.GetTotalGeneratedSourceLength(unorderedCompilation);
    }
}
