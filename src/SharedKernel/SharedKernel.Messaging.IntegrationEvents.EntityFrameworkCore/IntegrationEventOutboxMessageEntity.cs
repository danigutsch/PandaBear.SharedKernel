namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Represents a durable outbound integration-event message waiting for publication.
/// </summary>
internal sealed class IntegrationEventOutboxMessageEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventOutboxMessageEntity" /> class.
    /// </summary>
    /// <param name="id">The outbox message identifier.</param>
    /// <param name="envelope">The serialized event envelope.</param>
    /// <param name="enqueuedAt">The time at which the message was enqueued.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id" /> is empty.</exception>
    public IntegrationEventOutboxMessageEntity(
        Guid id,
        EventEnvelope envelope,
        DateTimeOffset enqueuedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Outbox message id must not be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(envelope);

        Id = id;
        Envelope = envelope;
        EnqueuedAt = enqueuedAt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventOutboxMessageEntity" /> class for EF Core.
    /// </summary>
    private IntegrationEventOutboxMessageEntity()
    {
        Envelope = null!;
    }

    /// <summary>
    /// Gets the outbox message identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the serialized event envelope.
    /// </summary>
    public EventEnvelope Envelope { get; private set; }

    /// <summary>
    /// Gets when the message was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; private set; }

    /// <summary>
    /// Gets when the message was published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// Marks the message as published.
    /// </summary>
    /// <param name="publishedAt">The time at which the message was published.</param>
    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
