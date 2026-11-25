using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Reports the scenario and parameter conditions for each benchmark case.
/// </summary>
internal sealed class BenchmarkConditionsColumn : IColumn
{
    /// <inheritdoc />
    public string Id => nameof(BenchmarkConditionsColumn);

    /// <inheritdoc />
    public string ColumnName => "Conditions";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 100;

    /// <inheritdoc />
    public bool IsNumeric => false;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Dimensionless;

    /// <inheritdoc />
    public string Legend => "Scenario and parameter conditions for the benchmark case.";

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var scenario = benchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
        var parameters = benchmarkCase.Parameters.PrintInfo;

        return string.IsNullOrWhiteSpace(parameters)
            ? scenario
            : $"{scenario}; {parameters}";
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
