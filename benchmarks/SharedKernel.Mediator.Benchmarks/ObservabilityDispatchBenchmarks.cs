using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Measures dispatch overhead for optional mediator observability under different listener states.
/// </summary>
[MemoryDiagnoser]
public class ObservabilityDispatchBenchmarks
{
    /// <summary>
    /// The dispatch baseline with no observability behavior.
    /// </summary>
    public const string ObservabilityDisabled = "Disabled";

    /// <summary>
    /// The dispatch path with the observability behavior enabled but no listener attached.
    /// </summary>
    public const string EnabledWithoutListener = "EnabledWithoutListener";

    /// <summary>
    /// The dispatch path with the observability behavior enabled and an active listener attached.
    /// </summary>
    public const string EnabledWithListener = "EnabledWithListener";

    private Func<CancellationToken, ValueTask<int>> withoutObservability = null!;
    private Func<CancellationToken, ValueTask<int>> withObservability = null!;
    private ActivityListener? listener;

    /// <summary>
    /// Gets or sets the compared observability mode.
    /// </summary>
    [Params(ObservabilityDisabled, EnabledWithoutListener, EnabledWithListener)]
    public string Mode { get; set; } = ObservabilityDisabled;

    /// <summary>
    /// Builds the synthetic benchmark assembly and configures the requested listener mode.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var benchmarkSource = ObservabilityDispatchBenchmarkSourceFactory.Source;
        var assemblyName = $"SharedKernel.Mediator.Benchmarks.Observability.{Mode}";
        var assembly = BenchmarkCompilationFactory.LoadAssembly(benchmarkSource, assemblyName);

        withoutObservability = CreateExport(assembly, "CreateWithoutObservability");
        withObservability = CreateExport(assembly, "CreateWithObservability");

        if (string.Equals(Mode, EnabledWithListener, StringComparison.Ordinal))
        {
            listener = CreateListener();
            ActivitySource.AddActivityListener(listener);
        }
    }

    /// <summary>
    /// Cleans up any listener registered for the active benchmark case.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        listener?.Dispose();
        listener = null;
    }

    /// <summary>
    /// Measures the configured observability dispatch mode.
    /// </summary>
    /// <returns>The handled response.</returns>
    [Benchmark]
    public ValueTask<int> Dispatch()
    {
        return string.Equals(Mode, ObservabilityDisabled, StringComparison.Ordinal)
            ? withoutObservability(CancellationToken.None)
            : withObservability(CancellationToken.None);
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

    private static ActivityListener CreateListener()
    {
        return new ActivityListener
        {
            ShouldListenTo = static source => source.Name == SharedKernelMediatorActivitySource.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
    }
}
