namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Represents a durable integration-event transport message for one consumer.
/// </summary>
internal sealed class IntegrationEventTransportMessage : EventEnvelope, IIntegrationEventInboxMessage
{
    /// <summary>Maximum stored length of the last consume error; longer text is truncated.</summary>
    internal const int LastConsumeErrorMaxLength = 2000;

    /// <summary>Maximum stored length of the claim owner name; longer names are truncated.</summary>
    internal const int ClaimOwnerMaxLength = 100;

    /// <summary>Maximum stored length of the consumer name.</summary>
    internal const int ConsumerNameMaxLength = 200;

    /// <summary>
    /// Creates a transport message that wraps the received envelope for one consumer.
    /// </summary>
    /// <param name="id">Identifier of the transport row.</param>
    /// <param name="consumerName">Name of the consumer the message is addressed to.</param>
    /// <param name="envelope">Envelope copied into the message.</param>
    /// <param name="receivedAt">When the message was stored.</param>
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

    /// <summary>Identifier of the integration event this message records.</summary>
    public Guid Id
    {
        get; private set;
    }

    /// <summary>Name of the consumer the message is recorded for, truncated to the maximum length.</summary>
    public string ConsumerName { get; private set; } = string.Empty;

    /// <summary>When the message was received.</summary>
    public DateTimeOffset ReceivedAt
    {
        get; private set;
    }

    /// <summary>When the consumer finished processing the message, or null while it is unprocessed.</summary>
    public DateTimeOffset? ProcessedAt
    {
        get; private set;
    }

    /// <summary>Number of times consuming the message has been attempted.</summary>
    public int ConsumeAttempts
    {
        get; private set;
    }

    /// <summary>When the latest consume attempt happened, or null before the first attempt.</summary>
    public DateTimeOffset? LastConsumeAttemptAt
    {
        get; private set;
    }

    /// <summary>Earliest time the next consume attempt may run, or null when none is scheduled.</summary>
    public DateTimeOffset? NextConsumeAttemptAt
    {
        get; private set;
    }

    /// <summary>Full type name of the exception from the latest failed consume attempt, or null when none failed. Exception messages are not stored because they can embed payload or personal data.</summary>
    public string? LastConsumeError
    {
        get; private set;
    }

    /// <summary>
    /// Gets when the message exhausted its consume attempts. Ordinary claims never select a poisoned message.
    /// </summary>
    public DateTimeOffset? PoisonedAt
    {
        get; private set;
    }

    /// <summary>Identifier of the consumer instance that currently holds the claim, or null when unclaimed.</summary>
    public string? ClaimedBy
    {
        get; private set;
    }

    /// <summary>When the current claim expires and another consumer may claim the message, or null when unclaimed.</summary>
    public DateTimeOffset? ClaimedUntil
    {
        get; private set;
    }

    /// <summary>
    /// Marks the message as consumed and clears retry and claim state.
    /// </summary>
    /// <param name="processedAt">When consumption succeeded.</param>
    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        LastConsumeAttemptAt = processedAt;
        NextConsumeAttemptAt = null;
        LastConsumeError = null;
        ClaimedBy = null;
        ClaimedUntil = null;
    }

    /// <summary>Records a final failed attempt and moves the message to the poison state.</summary>
    /// <param name="attemptedAt">When the final attempt failed.</param>
    /// <param name="error">The bounded failure description.</param>
    public void MarkPoisoned(DateTimeOffset attemptedAt, string error)
    {
        MarkConsumeFailed(attemptedAt, attemptedAt, error);
        NextConsumeAttemptAt = null;
        PoisonedAt = attemptedAt;
    }

    /// <summary>
    /// Returns a poisoned message to ordinary delivery with a fresh attempt budget, for an audited operator replay.
    /// </summary>
    /// <param name="releasedAt">When the message becomes claimable again.</param>
    public void ReleaseFromPoison(DateTimeOffset releasedAt)
    {
        if (PoisonedAt is null)
        {
            throw new InvalidOperationException("Only a poisoned transport message can be released.");
        }

        PoisonedAt = null;
        ConsumeAttempts = 0;
        NextConsumeAttemptAt = releasedAt;
    }

    /// <summary>
    /// Records a failed consume attempt, truncates the error, and releases the claim so the message is retried.
    /// </summary>
    /// <param name="attemptedAt">When the attempt failed.</param>
    /// <param name="nextAttemptAt">When the message becomes eligible again.</param>
    /// <param name="error">The failure description.</param>
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
