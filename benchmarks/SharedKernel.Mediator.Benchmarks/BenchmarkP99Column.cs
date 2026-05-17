using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the 99th-percentile execution time for each benchmark case.
/// </summary>
internal sealed class BenchmarkP99Column : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(BenchmarkP99Column);

    /// <inheritdoc />
    public string ColumnName => "P99";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Statistics;

    /// <inheritdoc />
    public int PriorityInCategory => 101;

    /// <inheritdoc />
    public bool IsNumeric => false;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Dimensionless;

    /// <inheritdoc />
    public string Legend => "99th-percentile execution time in nanoseconds.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        return GetValue(summary, benchmarkCase, summary.Style);
    }

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        var statistics = report?.ResultStatistics;

        if (statistics is null)
        {
            return "n/a";
        }

        var percentile = statistics.Percentiles.Percentile(99);
        return style.PrintUnitsInContent
            ? string.Format(style.CultureInfo, "{0:N2} ns", percentile)
            : percentile.ToString("N2", style.CultureInfo);
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
