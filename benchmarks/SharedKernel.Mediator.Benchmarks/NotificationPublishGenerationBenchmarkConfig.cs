namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Configures the notification-publish generation benchmark summary columns.
/// </summary>
internal sealed class NotificationPublishGenerationBenchmarkConfig : BenchmarkOutputConfig
{
    /// <summary>
    /// Initializes the notification-publish generation benchmark configuration.
    /// </summary>
    public NotificationPublishGenerationBenchmarkConfig()
    {
        AddColumn(new NotificationPublishGeneratedSourceSizeColumn());
    }
}
