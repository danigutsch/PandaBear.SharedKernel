namespace SharedKernel.Messaging;

/// <summary>
/// Carries event envelope metadata, payload content, and extension attributes across messaging boundaries.
/// </summary>
public class EventEnvelope : IEventEnvelope
{
    /// <summary>
    /// The maximum supported event type identifier length.
    /// </summary>
    public const int EventTypeMaxLength = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventEnvelope" /> class.
    /// </summary>
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
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="envelopeSpec" />, <paramref name="envelopeSpecVersion" />,
    /// <paramref name="eventId" />, or <paramref name="eventType" /> is blank, or when
    /// <paramref name="eventType" /> is too long.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="eventVersion" /> is less than or equal to zero.
    /// </exception>
    public EventEnvelope(
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
        string? extensionAttributesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeSpec);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeSpecVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (eventType.Length > EventTypeMaxLength)
        {
            throw new ArgumentException($"Event type must not exceed {EventTypeMaxLength} characters.", nameof(eventType));
        }

        if (eventVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventVersion), eventVersion, "Event version must be greater than zero.");
        }

        if (payload is not null && string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload must not be blank when provided.", nameof(payload));
        }

        EnvelopeSpec = envelopeSpec;
        EnvelopeSpecVersion = envelopeSpecVersion;
        EventId = eventId;
        Source = source;
        EventType = eventType;
        EventVersion = eventVersion;
        Time = time;
        Subject = subject;
        DataContentType = dataContentType;
        DataSchema = dataSchema;
        Payload = payload;
        PayloadEncoding = payloadEncoding;
        ExtensionAttributesJson = extensionAttributesJson;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventEnvelope" /> class for serializers and mappers.
    /// </summary>
    protected EventEnvelope()
    {
        EnvelopeSpec = string.Empty;
        EnvelopeSpecVersion = string.Empty;
        EventId = string.Empty;
        Source = new Uri("urn:empty");
        EventType = string.Empty;
    }

    /// <summary>
    /// Gets the envelope specification name.
    /// </summary>
    public string EnvelopeSpec { get; private set; }

    /// <summary>
    /// Gets the envelope specification version.
    /// </summary>
    public string EnvelopeSpecVersion { get; private set; }

    /// <summary>
    /// Gets the event identifier used for idempotency and tracing.
    /// </summary>
    public string EventId { get; private set; }

    /// <summary>
    /// Gets the event source.
    /// </summary>
    public Uri Source { get; private set; }

    /// <summary>
    /// Gets the stable event type identifier.
    /// </summary>
    public string EventType { get; private set; }

    /// <summary>
    /// Gets the optional event contract version.
    /// </summary>
    public int? EventVersion { get; private set; }

    /// <summary>
    /// Gets the time at which the event occurred.
    /// </summary>
    public DateTimeOffset? Time { get; private set; }

    /// <summary>
    /// Gets the optional event subject.
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>
    /// Gets the optional payload content type.
    /// </summary>
    public string? DataContentType { get; private set; }

    /// <summary>
    /// Gets the optional payload schema.
    /// </summary>
    public Uri? DataSchema { get; private set; }

    /// <summary>
    /// Gets the optional serialized event payload.
    /// </summary>
    public string? Payload { get; private set; }

    /// <summary>
    /// Gets the payload encoding.
    /// </summary>
    public EventPayloadEncoding PayloadEncoding { get; private set; }

    /// <summary>
    /// Gets the optional serialized extension attribute object.
    /// </summary>
    public string? ExtensionAttributesJson { get; private set; }
}
