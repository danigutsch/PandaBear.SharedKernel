using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures full-enumeration costs for the stream-dispatch strategies under comparison.
/// </summary>
[Config(typeof(StreamDispatchEnumerationBenchmarkConfig))]
[MemoryDiagnoser]
public class StreamDispatchEnumerationBenchmarks
{
    private Func<CancellationToken, ValueTask<int>> directStreamHandler = null!;
    private Func<CancellationToken, ValueTask<int>> generatedMediatorDirectReturn = null!;
    private Func<CancellationToken, ValueTask<int>> generatedWrapper = null!;
    private Func<CancellationToken, ValueTask<int>> channelCopy = null!;
    private Func<CancellationToken, ValueTask<int>> bufferedCopy = null!;

    /// <summary>
    /// Gets or sets the number of stream items yielded by the synthetic source.
    /// </summary>
    [Params(0, 1, 10, 1000, 100000)]
    public int ItemCount { get; set; }

    /// <summary>
    /// Builds the synthetic benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = StreamDispatchBenchmarkSourceFactory.CreateSource(ItemCount);
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.Stream.Enumeration.{ItemCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);

        directStreamHandler = CreateExport(assembly, "CreateDirectStreamHandlerEnumeration");
        generatedMediatorDirectReturn = CreateExport(assembly, "CreateGeneratedMediatorDirectReturnEnumeration");
        generatedWrapper = CreateExport(assembly, "CreateGeneratedWrapperEnumeration");
        channelCopy = CreateExport(assembly, "CreateChannelCopyEnumeration");
        bufferedCopy = CreateExport(assembly, "CreateBufferedCopyEnumeration");
    }

    /// <summary>
    /// Measures direct stream-handler full enumeration.
    /// </summary>
    /// <returns>The total number of yielded items.</returns>
    [Benchmark(Baseline = true, Description = "Direct stream handler")]
    public ValueTask<int> DirectStreamHandler()
    {
        return directStreamHandler(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated typed direct-return path.
    /// </summary>
    /// <returns>The total number of yielded items.</returns>
    [Benchmark(Description = "Generated mediator direct return")]
    public ValueTask<int> GeneratedMediatorDirectReturn()
    {
        return generatedMediatorDirectReturn(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated generic wrapper path.
    /// </summary>
    /// <returns>The total number of yielded items.</returns>
    [Benchmark(Description = "Generated wrapper")]
    public ValueTask<int> GeneratedWrapper()
    {
        return generatedWrapper(CancellationToken.None);
    }

    /// <summary>
    /// Measures a channel-copy wrapper path.
    /// </summary>
    /// <returns>The total number of yielded items.</returns>
    [Benchmark(Description = "Channel copy")]
    public ValueTask<int> ChannelCopy()
    {
        return channelCopy(CancellationToken.None);
    }

    /// <summary>
    /// Measures a buffered-copy wrapper path.
    /// </summary>
    /// <returns>The total number of yielded items.</returns>
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
