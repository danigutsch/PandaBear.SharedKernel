using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the synthetic generated benchmark-source size for each dispatch-scale benchmark case.
/// </summary>
internal sealed class DispatchGeneratedSourceSizeColumn : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(DispatchGeneratedSourceSizeColumn);

    /// <inheritdoc />
    public string ColumnName => "Generated source size";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 0;

    /// <inheritdoc />
    public bool IsNumeric => true;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Size;

    /// <inheritdoc />
    public string Legend => "Synthetic benchmark source length in characters.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var requestCount = Convert.ToInt32(
            benchmarkCase.Parameters[nameof(DispatchScaleBenchmarks.RequestCount)],
            CultureInfo.InvariantCulture);
        var requestShape = Convert.ToString(
            benchmarkCase.Parameters[nameof(DispatchScaleBenchmarks.RequestShape)],
            CultureInfo.InvariantCulture) ?? DispatchBenchmarkSourceFactory.ClassShape;
        var pipelineCount = Convert.ToInt32(
            benchmarkCase.Parameters[nameof(DispatchScaleBenchmarks.PipelineCount)],
            CultureInfo.InvariantCulture);
        var sourceLength = DispatchBenchmarkSourceFactory.CreateSource(requestCount, requestShape, pipelineCount).Length;

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
}
