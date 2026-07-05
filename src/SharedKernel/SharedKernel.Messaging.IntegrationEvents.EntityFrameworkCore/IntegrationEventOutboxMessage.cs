namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Represents a durable outbound integration event message waiting for publication.
/// </summary>
internal sealed class IntegrationEventOutboxMessage : EventEnvelope, IIntegrationEventOutboxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventOutboxMessage" /> class.
    /// </summary>
    /// <param name="id">The outbox message identifier.</param>
    /// <param name="envelopeSpec">The envelope specification name.</param>
    /// <param name="envelopeSpecVersion">The envelope specification version.</param>
    /// <param name="eventId">The event identifier used for idempotency and tracing.</param>
    /// <param name="source">The event source.</param>
    /// <param name="eventType">The stable event type identifier.</param>
    /// <param name="eventVersion">The optional event contract version.</param>
    /// <param name="time">The time at which the event occurred.</param>
    /// <param name="subject">The optional event subject.</param>
    /// <param name="dataContentType">The optional payload content type.</param>
    /// <param name="dataSchema">The optional payload schema.</param>
    /// <param name="payload">The optional serialized event payload.</param>
    /// <param name="payloadEncoding">The payload encoding.</param>
    /// <param name="extensionAttributesJson">The optional serialized extension attribute object.</param>
    /// <param name="enqueuedAt">The time at which the message was enqueued.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id" /> is empty.</exception>
    public IntegrationEventOutboxMessage(
        Guid id,
        string envelopeSpec,
        string envelopeSpecVersion,
        string eventId,
        Uri source,
        string eventType,
        int? eventVersion,
        DateTimeOffset? time,
        string? subject,
        string? dataContentType,
        Uri? dataSchema,
        string? payload,
        EventPayloadEncoding payloadEncoding,
        string? extensionAttributesJson,
        DateTimeOffset enqueuedAt)
        : base(
            envelopeSpec,
            envelopeSpecVersion,
            eventId,
            source,
            eventType,
            eventVersion,
            time,
            subject,
            dataContentType,
            dataSchema,
            payload,
            payloadEncoding,
            extensionAttributesJson)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Outbox message id must not be empty.", nameof(id));
        }

        Id = id;
        EnqueuedAt = enqueuedAt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEventOutboxMessage" /> class for EF Core.
    /// </summary>
    private IntegrationEventOutboxMessage()
    {
    }

    /// <inheritdoc />
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset EnqueuedAt { get; private set; }

    /// <inheritdoc />
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
