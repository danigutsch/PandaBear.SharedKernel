using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System.Globalization;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports allocated bytes per streamed item for each full-enumeration benchmark case.
/// </summary>
internal sealed class StreamAllocatedPerItemColumn : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(StreamAllocatedPerItemColumn);

    /// <inheritdoc />
    public string ColumnName => "Allocated / item";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 0;

    /// <inheritdoc />
    public bool IsNumeric => false;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Dimensionless;

    /// <inheritdoc />
    public string Legend => "Allocated bytes per yielded item for the full-enumeration stream benchmark.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var itemCount = Convert.ToInt32(
            benchmarkCase.Parameters[nameof(StreamDispatchEnumerationBenchmarks.ItemCount)],
            CultureInfo.InvariantCulture);

        if (itemCount == 0)
        {
            return "n/a";
        }

        var report = summary[benchmarkCase];

        if (report is null)
        {
            return "n/a";
        }

        var allocatedMetric = report.Metrics.Values.FirstOrDefault(
            static metric => string.Equals(metric.Descriptor.DisplayName, "Allocated", StringComparison.Ordinal));

        if (allocatedMetric is null)
        {
            return "n/a";
        }

        var bytesPerOperation = allocatedMetric.Value;
        var bytesPerItem = bytesPerOperation / itemCount;
        return string.Create(CultureInfo.InvariantCulture, $"{bytesPerItem:N2} B");
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
