using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Observes how far a producer can advance while the consumer pauses after the first item.
/// </summary>
[Config(typeof(BenchmarkOutputConfig))]
[MemoryDiagnoser]
public class StreamDispatchBackpressureBenchmarks
{
    private Func<CancellationToken, ValueTask<int>> directStreamHandler = null!;
    private Func<CancellationToken, ValueTask<int>> generatedMediatorDirectReturn = null!;
    private Func<CancellationToken, ValueTask<int>> generatedWrapper = null!;
    private Func<CancellationToken, ValueTask<int>> channelCopy = null!;
    private Func<CancellationToken, ValueTask<int>> bufferedCopy = null!;

    /// <summary>
    /// Gets or sets the number of stream items yielded by the synthetic source.
    /// </summary>
    [Params(2, 10, 1000, 100000)]
    public int ItemCount { get; set; }

    /// <summary>
    /// Builds the synthetic benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = StreamDispatchBenchmarkSourceFactory.CreateSource(ItemCount);
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.Stream.Backpressure.{ItemCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);

        directStreamHandler = CreateExport(assembly, "CreateDirectStreamHandlerBackpressure");
        generatedMediatorDirectReturn = CreateExport(assembly, "CreateGeneratedMediatorDirectReturnBackpressure");
        generatedWrapper = CreateExport(assembly, "CreateGeneratedWrapperBackpressure");
        channelCopy = CreateExport(assembly, "CreateChannelCopyBackpressure");
        bufferedCopy = CreateExport(assembly, "CreateBufferedCopyBackpressure");
    }

    /// <summary>
    /// Observes producer progress for the direct stream-handler path while the consumer pauses.
    /// </summary>
    /// <returns>The produced-item count visible before the second pull.</returns>
    [Benchmark(Baseline = true, Description = "Direct stream handler")]
    public ValueTask<int> DirectStreamHandler()
    {
        return directStreamHandler(CancellationToken.None);
    }

    /// <summary>
    /// Observes producer progress for the generated typed direct-return path while the consumer pauses.
    /// </summary>
    /// <returns>The produced-item count visible before the second pull.</returns>
    [Benchmark(Description = "Generated mediator direct return")]
    public ValueTask<int> GeneratedMediatorDirectReturn()
    {
        return generatedMediatorDirectReturn(CancellationToken.None);
    }

    /// <summary>
    /// Observes producer progress for the generated generic wrapper path while the consumer pauses.
    /// </summary>
    /// <returns>The produced-item count visible before the second pull.</returns>
    [Benchmark(Description = "Generated wrapper")]
    public ValueTask<int> GeneratedWrapper()
    {
        return generatedWrapper(CancellationToken.None);
    }

    /// <summary>
    /// Observes producer progress for the channel-copy path while the consumer pauses.
    /// </summary>
    /// <returns>The produced-item count visible before the second pull.</returns>
    [Benchmark(Description = "Channel copy")]
    public ValueTask<int> ChannelCopy()
    {
        return channelCopy(CancellationToken.None);
    }

    /// <summary>
    /// Observes producer progress for the buffered-copy path while the consumer pauses.
    /// </summary>
    /// <returns>The produced-item count visible before the second pull.</returns>
    [Benchmark(Description = "Buffered copy")]
    public ValueTask<int> BufferedCopy()
    {
        return bufferedCopy(CancellationToken.None);
    }

    private static Func<CancellationToken, ValueTask<int>> CreateExport(Assembly assembly, string methodName)
    {
        var exportsType = assembly.GetType("BenchmarkApp.BenchmarkExports", throwOnError: true)!;
        var method = exportsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

        if (method?.Invoke(null, null) is Func<CancellationToken, ValueTask<int>> exportedDelegate)
        {
            return exportedDelegate;
        }

        throw new InvalidOperationException($"Benchmark export '{methodName}' was not found.");
    }
}
