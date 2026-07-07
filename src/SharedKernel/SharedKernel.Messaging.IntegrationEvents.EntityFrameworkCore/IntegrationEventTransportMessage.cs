namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Represents a durable integration-event transport message for one consumer.
/// </summary>
internal sealed class IntegrationEventTransportMessage : EventEnvelope, IIntegrationEventInboxMessage
{
    internal const int LastConsumeErrorMaxLength = 2000;

    internal const int ClaimOwnerMaxLength = 100;

    internal const int ConsumerNameMaxLength = 200;

    public IntegrationEventTransportMessage(
        Guid id,
        string consumerName,
        EventEnvelope envelope,
        DateTimeOffset receivedAt)
        : base(
            (envelope ?? throw new ArgumentNullException(nameof(envelope))).EnvelopeSpec,
            envelope.EnvelopeSpecVersion,
            envelope.EventId,
            envelope.Source,
            envelope.EventType,
            envelope.EventVersion,
            envelope.Time,
            envelope.Subject,
            envelope.DataContentType,
            envelope.DataSchema,
            envelope.Payload,
            envelope.PayloadEncoding,
            envelope.ExtensionAttributesJson)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Transport message id must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        Id = id;
        ConsumerName = consumerName.Length <= ConsumerNameMaxLength
            ? consumerName
            : consumerName[..ConsumerNameMaxLength];
        ReceivedAt = receivedAt;
    }

    private IntegrationEventTransportMessage()
    {
    }

    public Guid Id { get; private set; }

    public string ConsumerName { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int ConsumeAttempts { get; private set; }

    public DateTimeOffset? LastConsumeAttemptAt { get; private set; }

    public DateTimeOffset? NextConsumeAttemptAt { get; private set; }

    public string? LastConsumeError { get; private set; }

    public string? ClaimedBy { get; private set; }

    public DateTimeOffset? ClaimedUntil { get; private set; }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        LastConsumeAttemptAt = processedAt;
        NextConsumeAttemptAt = null;
        LastConsumeError = null;
        ClaimedBy = null;
        ClaimedUntil = null;
    }

    public void MarkConsumeFailed(DateTimeOffset attemptedAt, DateTimeOffset nextAttemptAt, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        ConsumeAttempts++;
        LastConsumeAttemptAt = attemptedAt;
        NextConsumeAttemptAt = nextAttemptAt;
        LastConsumeError = error.Length <= LastConsumeErrorMaxLength
            ? error
            : error[..LastConsumeErrorMaxLength];
        ClaimedBy = null;
        ClaimedUntil = null;
    }
}
