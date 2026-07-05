namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Represents a durable integration event waiting for publication.
/// </summary>
internal sealed class IntegrationEventOutboxMessage
{
    /// <summary>
    /// Gets or sets the outbox message identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the integration event type identifier.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the integration event contract version.
    /// </summary>
    public int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the integration event identifier.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets when the integration event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Gets or sets the serialized integration event payload.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the message was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; set; }

    /// <summary>
    /// Gets or sets when the message was published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
