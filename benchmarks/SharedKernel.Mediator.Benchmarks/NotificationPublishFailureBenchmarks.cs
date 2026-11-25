using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures exceptional and canceled notification publish paths for sequential and parallel strategies.
/// </summary>
[Config(typeof(BenchmarkOutputConfig))]
[MemoryDiagnoser]
public class NotificationPublishFailureBenchmarks
{
    private Func<ValueTask<int>> exceptionPath = null!;
    private Func<ValueTask<int>> cancellationPath = null!;

    /// <summary>
    /// Gets or sets the publish strategy under test.
    /// </summary>
    [Params(NotificationPublishBenchmarkSourceFactory.SequentialStrategy, NotificationPublishBenchmarkSourceFactory.ParallelStrategy)]
    public string Strategy { get; set; } = NotificationPublishBenchmarkSourceFactory.SequentialStrategy;

    /// <summary>
    /// Gets or sets the number of handlers emitted for failure-path scenarios.
    /// </summary>
    [Params(NotificationPublishBenchmarkSourceFactory.DefaultFailureHandlerCount)]
    public int HandlerCount { get; set; } = NotificationPublishBenchmarkSourceFactory.DefaultFailureHandlerCount;

    /// <summary>
    /// Builds the synthetic benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = NotificationPublishBenchmarkSourceFactory.CreateRuntimeSource(HandlerCount, HandlerCount);
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.NotificationPublish.Failures.{Strategy}.{HandlerCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);
        var exceptionExport = Strategy == NotificationPublishBenchmarkSourceFactory.SequentialStrategy
            ? "CreateSequentialExceptionPath"
            : "CreateParallelExceptionPath";
        var cancellationExport = Strategy == NotificationPublishBenchmarkSourceFactory.SequentialStrategy
            ? "CreateSequentialCancellationPath"
            : "CreateParallelCancellationPath";

        exceptionPath = CreateExport<Func<ValueTask<int>>>(assembly, exceptionExport);
        cancellationPath = CreateExport<Func<ValueTask<int>>>(assembly, cancellationExport);
    }

    /// <summary>
    /// Measures the exception publish path while absorbing the thrown exception.
    /// </summary>
    /// <returns><c>1</c> when the expected exception is observed.</returns>
    [Benchmark(Baseline = true, Description = "Exception path")]
    public ValueTask<int> ExceptionPath()
    {
        return exceptionPath();
    }

    /// <summary>
    /// Measures the cancellation publish path while absorbing the thrown cancellation exception.
    /// </summary>
    /// <returns><c>1</c> when the expected cancellation is observed.</returns>
    [Benchmark(Description = "Cancellation path")]
    public ValueTask<int> CancellationPath()
    {
        return cancellationPath();
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
