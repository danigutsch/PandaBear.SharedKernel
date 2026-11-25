using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Configures the shared summary columns required across benchmark reports.
/// </summary>
internal class BenchmarkOutputConfig : ManualConfig
{
    /// <summary>
    /// Initializes the benchmark output configuration.
    /// </summary>
    public BenchmarkOutputConfig()
    {
        AddColumn(StatisticColumn.Median);
        AddColumn(StatisticColumn.P95);
        AddColumn(new BenchmarkP99Column());
        AddColumn(new BenchmarkConditionsColumn());
    }
}
