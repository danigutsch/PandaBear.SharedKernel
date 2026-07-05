namespace SharedKernel.Messaging;

/// <summary>
/// Carries serialized event identity, version, occurrence time, and payload metadata across messaging boundaries.
/// </summary>
public sealed class EventEnvelope
{
    /// <summary>
    /// The maximum supported event type identifier length.
    /// </summary>
    public const int EventTypeMaxLength = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventEnvelope" /> class.
    /// </summary>
    /// <param name="eventId">The event identifier used for idempotency and tracing.</param>
    /// <param name="eventType">The stable event type identifier.</param>
    /// <param name="eventVersion">The event contract version.</param>
    /// <param name="occurredAt">The time at which the event occurred.</param>
    /// <param name="payloadJson">The serialized JSON event payload.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventId" /> is empty, <paramref name="eventType" /> is blank,
    /// <paramref name="eventType" /> is too long, or <paramref name="payloadJson" /> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="eventVersion" /> is less than or equal to zero.
    /// </exception>
    public EventEnvelope(
        Guid eventId,
        string eventType,
        int eventVersion,
        DateTimeOffset occurredAt,
        string payloadJson)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id must not be empty.", nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (eventType.Length > EventTypeMaxLength)
        {
            throw new ArgumentException($"Event type must not exceed {EventTypeMaxLength} characters.", nameof(eventType));
        }

        if (eventVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventVersion), eventVersion, "Event version must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        EventId = eventId;
        EventType = eventType;
        EventVersion = eventVersion;
        OccurredAt = occurredAt;
        PayloadJson = payloadJson;
    }

    /// <summary>
    /// Gets the event identifier used for idempotency and tracing.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the stable event type identifier.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the event contract version.
    /// </summary>
    public int EventVersion { get; }

    /// <summary>
    /// Gets the time at which the event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Gets the serialized JSON event payload.
    /// </summary>
    public string PayloadJson { get; }
}
