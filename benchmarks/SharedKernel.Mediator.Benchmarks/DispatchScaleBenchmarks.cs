using BenchmarkDotNet.Attributes;
using System.Reflection;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures dispatch-shape costs across request-count scale points for the current no-pipeline path.
/// </summary>
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

    private Func<CancellationToken, ValueTask<int>> directHandlerCall = null!;
    private Func<CancellationToken, ValueTask<int>> generatedTypedOverload = null!;
    private Func<CancellationToken, ValueTask<int>> generatedGenericSwitch = null!;
    private Func<CancellationToken, ValueTask<object?>> generatedObjectSwitch = null!;

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
    /// Builds the generated benchmark assembly and captures the exported delegates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.DispatchScale.{RequestShape}.{RequestCount}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(
            DispatchBenchmarkSourceFactory.CreateSource(RequestCount, RequestShape),
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
