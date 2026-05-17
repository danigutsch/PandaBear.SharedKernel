namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Configures the stream-enumeration benchmark summary columns.
/// </summary>
internal sealed class StreamDispatchEnumerationBenchmarkConfig : BenchmarkOutputConfig
{
    /// <summary>
    /// Initializes the stream-enumeration benchmark configuration.
    /// </summary>
    public StreamDispatchEnumerationBenchmarkConfig()
    {
        AddColumn(new StreamAllocatedPerItemColumn());
    }
}
