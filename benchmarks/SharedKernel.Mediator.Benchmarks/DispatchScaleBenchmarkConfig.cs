namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Configures the dispatch-scale benchmark summary columns.
/// </summary>
internal sealed class DispatchScaleBenchmarkConfig : BenchmarkOutputConfig
{
    /// <summary>
    /// Initializes the dispatch-scale benchmark configuration.
    /// </summary>
    public DispatchScaleBenchmarkConfig()
    {
        AddColumn(new DispatchGeneratedSourceSizeColumn());
    }
}
