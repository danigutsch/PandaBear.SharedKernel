using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures dispatch-shape costs across request-count scale points and pipeline-count variants.
/// </summary>
[Config(typeof(DispatchScaleBenchmarkConfig))]
[DisassemblyDiagnoser(maxDepth: 0)]
[MemoryDiagnoser]
public class DispatchScaleBenchmarks
{
    /// <summary>
    /// The class-request benchmark shape.
    /// </summary>
    public const string ClassShape = DispatchBenchmarkSourceFactory.ClassShape;

    /// <summary>
    /// The record-class benchmark shape.
    /// </summary>
    public const string RecordClassShape = DispatchBenchmarkSourceFactory.RecordClassShape;

    /// <summary>
    /// The readonly-record-struct benchmark shape.
    /// </summary>
    public const string ReadonlyRecordStructShape = DispatchBenchmarkSourceFactory.ReadonlyRecordStructShape;

    /// <summary>
    /// The explicit zero-pipeline dispatch baseline.
    /// </summary>
    public const int NoPipelineCount = DispatchBenchmarkSourceFactory.NoPipelineCount;

    /// <summary>
    /// The one-pipeline dispatch variant.
    /// </summary>
    public const int OnePipelineCount = DispatchBenchmarkSourceFactory.OnePipelineCount;

    /// <summary>
    /// The three-pipeline dispatch variant.
    /// </summary>
    public const int ThreePipelineCount = DispatchBenchmarkSourceFactory.ThreePipelineCount;

    /// <summary>
    /// The ten-pipeline dispatch variant.
    /// </summary>
    public const int TenPipelineCount = DispatchBenchmarkSourceFactory.TenPipelineCount;

    private Func<CancellationToken, ValueTask<int>> directHandlerCall = null!;
    private Func<CancellationToken, ValueTask<int>> generatedTypedOverload = null!;
    private Func<CancellationToken, ValueTask<int>> generatedGenericSwitch = null!;
    private Func<CancellationToken, ValueTask<object?>> generatedObjectSwitch = null!;
    private string benchmarkSource = string.Empty;
    private string assemblyName = string.Empty;

    /// <summary>
    /// Gets or sets the number of generated request types under test.
    /// </summary>
    [Params(1, 10, 100, 1000, 5000)]
    public int RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the request shape under test.
    /// </summary>
    [Params(ClassShape, RecordClassShape, ReadonlyRecordStructShape)]
    public string RequestShape { get; set; } = ClassShape;

    /// <summary>
    /// Gets or sets the number of pipeline behaviors emitted per request.
    /// </summary>
    [Params(NoPipelineCount, OnePipelineCount, ThreePipelineCount, TenPipelineCount)]
    public int PipelineCount { get; set; } = OnePipelineCount;

    /// <summary>
    /// Builds the generated benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        benchmarkSource = DispatchBenchmarkSourceFactory.CreateSource(RequestCount, RequestShape, PipelineCount);
        assemblyName = $"SharedKernel.Mediator.Benchmarks.DispatchScale.{RequestShape}.P{PipelineCount}.{RequestCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(
            benchmarkSource,
            assemblyName);

        directHandlerCall = CreateExport<Func<CancellationToken, ValueTask<int>>>(assembly, "CreateDirectHandlerCall");
        generatedTypedOverload = CreateExport<Func<CancellationToken, ValueTask<int>>>(assembly, "CreateGeneratedTypedOverload");
        generatedGenericSwitch = CreateExport<Func<CancellationToken, ValueTask<int>>>(assembly, "CreateGeneratedGenericSwitch");
        generatedObjectSwitch = CreateExport<Func<CancellationToken, ValueTask<object?>>>(assembly, "CreateGeneratedObjectSwitch");
    }

    /// <summary>
    /// Measures the direct handler baseline.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Baseline = true, Description = "Direct handler call")]
    public ValueTask<int> DirectHandlerCall()
    {
        return directHandlerCall(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated typed-overload path.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Generated typed overload")]
    public ValueTask<int> GeneratedTypedOverload()
    {
        return generatedTypedOverload(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated generic-switch path.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark(Description = "Generated generic switch dispatch")]
    public ValueTask<int> GeneratedGenericSwitch()
    {
        return generatedGenericSwitch(CancellationToken.None);
    }

    /// <summary>
    /// Measures the generated object-switch path.
    /// </summary>
    /// <returns>The boxed handled response.</returns>
    [Benchmark(Description = "Generated object switch dispatch")]
    public ValueTask<object?> GeneratedObjectSwitch()
    {
        return generatedObjectSwitch(CancellationToken.None);
    }

    /// <summary>
    /// Measures the synthetic benchmark-assembly build path for the current dispatch case.
    /// </summary>
    /// <returns>The emitted benchmark assembly size in bytes.</returns>
    [Benchmark(Description = "Generated benchmark assembly build time")]
    public int BuildGeneratedAssembly()
    {
        return BenchmarkCompilationFactory.GetEmittedAssemblySize(benchmarkSource, assemblyName);
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
