namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Publication metadata for one retained outbox row.</summary>
public sealed class IntegrationEventOutboxDelivery
{
    /// <summary>Gets the durable storage-row identifier.</summary>
    public required Guid StorageId { get; init; }

    /// <summary>Gets the envelope identifier used by the producer.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets the envelope's stable event type.</summary>
    public required string EventType { get; init; }

    /// <summary>Gets when the row was recorded in the outbox.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets when publication succeeded, or <see langword="null"/> while pending.</summary>
    public required DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Gets the number of failed publication attempts.</summary>
    public required int FailedAttempts { get; init; }

    /// <summary>Gets the time of the last publication attempt, when available.</summary>
    public required DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>Gets the time of the next scheduled retry, when available.</summary>
    public required DateTimeOffset? NextAttemptAt { get; init; }
}
