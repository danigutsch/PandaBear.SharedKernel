using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures sequential and parallel notification publish costs across handler-count scale points.
/// </summary>
[MemoryDiagnoser]
public class NotificationPublishBenchmarks
{
    private Func<CancellationToken, ValueTask> sequentialPublish = null!;
    private Func<CancellationToken, ValueTask> parallelPublish = null!;

    /// <summary>
    /// Gets or sets the number of notification handlers under test.
    /// </summary>
    [Params(0, 1, 3, 10, 50)]
    public int HandlerCount { get; set; }

    /// <summary>
    /// Builds the synthetic benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = NotificationPublishBenchmarkSourceFactory.CreateRuntimeSource(HandlerCount);
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.NotificationPublish.{HandlerCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);

        sequentialPublish = CreateExport<Func<CancellationToken, ValueTask>>(assembly, "CreateSequentialPublish");
        parallelPublish = CreateExport<Func<CancellationToken, ValueTask>>(assembly, "CreateParallelPublish");
    }

    /// <summary>
    /// Measures generated-style sequential notification publish.
    /// </summary>
    /// <returns>The publish operation.</returns>
    [Benchmark(Baseline = true, Description = "Sequential publish")]
    public ValueTask SequentialPublish()
    {
        return sequentialPublish(CancellationToken.None);
    }

    /// <summary>
    /// Measures generated-style parallel notification publish.
    /// </summary>
    /// <returns>The publish operation.</returns>
    [Benchmark(Description = "Parallel publish")]
    public ValueTask ParallelPublish()
    {
        return parallelPublish(CancellationToken.None);
    }

    private static TDelegate CreateExport<TDelegate>(Assembly assembly, string methodName)
        where TDelegate : Delegate
    {
        var exportsType = assembly.GetType("BenchmarkApp.BenchmarkExports", throwOnError: true)!;
        var method = exportsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

        if (method?.Invoke(null, null) is TDelegate exportedDelegate)
        {
            return exportedDelegate;
        }

        throw new InvalidOperationException($"Benchmark export '{methodName}' was not found.");
    }
}
