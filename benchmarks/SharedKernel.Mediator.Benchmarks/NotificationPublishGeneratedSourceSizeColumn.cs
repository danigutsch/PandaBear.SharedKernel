using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp;
using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the generated source size for each notification-publish generation benchmark case.
/// </summary>
internal sealed class NotificationPublishGeneratedSourceSizeColumn : IColumn
{
    private const string OrderedGenerationScenario = "Ordered generation overhead";

    /// <inheritdoc />
    public string Id => nameof(NotificationPublishGeneratedSourceSizeColumn);

    /// <inheritdoc />
    public string ColumnName => "Generated source size";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 1;

    /// <inheritdoc />
    public bool IsNumeric => true;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Size;

    /// <inheritdoc />
    public string Legend => "Total generated source length in characters emitted by notification publish generation.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var compilation = CreateCompilationForScenario(benchmarkCase);
        var sourceLength = BenchmarkCompilationFactory.GetTotalGeneratedSourceLength(compilation);
        return sourceLength.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        return GetValue(summary, benchmarkCase);
    }

    /// <inheritdoc />
    public bool IsAvailable(Summary summary)
    {
        return true;
    }

    /// <inheritdoc />
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase)
    {
        return false;
    }

    private static CSharpCompilation CreateCompilationForScenario(BenchmarkCase benchmarkCase)
    {
        var handlerCount = Convert.ToInt32(
            benchmarkCase.Parameters[nameof(NotificationPublishGenerationBenchmarks.HandlerCount)],
            CultureInfo.InvariantCulture);
        var scenario = benchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
        var ordering = string.Equals(scenario, OrderedGenerationScenario, StringComparison.Ordinal)
            ? NotificationPublishBenchmarkSourceFactory.OrderedHandlers
            : NotificationPublishBenchmarkSourceFactory.UnorderedHandlers;
        var source = NotificationPublishBenchmarkSourceFactory.CreateGenerationSource(handlerCount, ordering);
        return BenchmarkCompilationFactory.CreateCompilation(source);
    }
}
