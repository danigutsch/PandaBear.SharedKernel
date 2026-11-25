using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures pipeline-dispatch strategy costs across pipeline depth, completion mode, and request shape.
/// </summary>
[Config(typeof(BenchmarkOutputConfig))]
[DisassemblyDiagnoser(maxDepth: 0)]
[MemoryDiagnoser]
public class PipelineDispatchBenchmarks
{
    private Func<CancellationToken, ValueTask<int>> noPipeline = null!;
    private Func<CancellationToken, ValueTask<int>> delegateChain = null!;
    private Func<CancellationToken, ValueTask<int>> staticDelegateChain = null!;
    private Func<CancellationToken, ValueTask<int>> generatedNestedCalls = null!;
    private Func<CancellationToken, ValueTask<int>> chainObject = null!;
    private Func<CancellationToken, ValueTask<int>> staticGenericPipeline = null!;

    /// <summary>
    /// Gets or sets the compared request shape.
    /// </summary>
    [Params(PipelineBenchmarkSourceFactory.ClassShape, PipelineBenchmarkSourceFactory.StructShape)]
    public string RequestShape { get; set; } = PipelineBenchmarkSourceFactory.ClassShape;

    /// <summary>
    /// Gets or sets the handler completion mode.
    /// </summary>
    [Params(PipelineBenchmarkSourceFactory.SynchronousCompletion, PipelineBenchmarkSourceFactory.AsynchronousCompletion)]
    public string CompletionMode { get; set; } = PipelineBenchmarkSourceFactory.SynchronousCompletion;

    /// <summary>
    /// Gets or sets the pipeline depth under comparison.
    /// </summary>
    [Params(0, 1, 3, 5, 10)]
    public int PipelineDepth { get; set; }

    /// <summary>
    /// Builds the synthetic benchmark assembly and captures the exported strategy delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = PipelineBenchmarkSourceFactory.CreateSource(RequestShape, PipelineDepth, CompletionMode);
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.Pipeline.{RequestShape}.{CompletionMode}.D{PipelineDepth}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);

        noPipeline = CreateExport(assembly, "CreateNoPipeline");
        delegateChain = CreateExport(assembly, "CreateDelegateChain");
        staticDelegateChain = CreateExport(assembly, "CreateStaticDelegateChain");
        generatedNestedCalls = CreateExport(assembly, "CreateGeneratedNestedCalls");
        chainObject = CreateExport(assembly, "CreateChainObject");
        staticGenericPipeline = CreateExport(assembly, "CreateStaticGenericPipeline");
    }

    /// <summary>
    /// Measures direct handler execution with no pipeline.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Baseline = true, Description = "No pipeline")]
    public ValueTask<int> NoPipeline()
    {
        return noPipeline(CancellationToken.None);
    }

    /// <summary>
    /// Measures per-call delegate-chain composition.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Delegate chain")]
    public ValueTask<int> DelegateChain()
    {
        return delegateChain(CancellationToken.None);
    }

    /// <summary>
    /// Measures a delegate chain composed once and reused across calls.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Static delegate chain")]
    public ValueTask<int> StaticDelegateChain()
    {
        return staticDelegateChain(CancellationToken.None);
    }

    /// <summary>
    /// Measures generated nested pipeline calls.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Generated nested calls")]
    public ValueTask<int> GeneratedNestedCalls()
    {
        return generatedNestedCalls(CancellationToken.None);
    }

    /// <summary>
    /// Measures a stateful chain-object dispatcher.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Chain object")]
    public ValueTask<int> ChainObject()
    {
        return chainObject(CancellationToken.None);
    }

    /// <summary>
    /// Measures a static generic pipeline shape.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Static generic pipeline")]
    public ValueTask<int> StaticGenericPipeline()
    {
        return staticGenericPipeline(CancellationToken.None);
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
