using BenchmarkDotNet.Configs;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Configures the discovery benchmark summary columns.
/// </summary>
internal sealed class DiscoveryBenchmarkConfig : ManualConfig
{
    /// <summary>
    /// Initializes the discovery benchmark configuration.
    /// </summary>
    public DiscoveryBenchmarkConfig()
    {
        AddColumn(new DiscoveryGeneratedSourceCountColumn());
        AddColumn(new DiscoveryGeneratedSourceSizeColumn());
    }
}
