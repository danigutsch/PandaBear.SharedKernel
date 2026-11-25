namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Delivery metadata for an unfinished outbox or transport row.</summary>
public sealed class IntegrationEventDeliveryEntry
{
    /// <summary>Gets the durable storage-row identifier.</summary>
    public required Guid StorageId { get; init; }

    /// <summary>Gets the envelope identifier used by the producer.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets the envelope's stable event type.</summary>
    public required string EventType { get; init; }

    /// <summary>Gets when the row was recorded in the outbox or transport.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>Gets the number of delivery attempts.</summary>
    public required int Attempts { get; init; }

    /// <summary>Gets the time of the last delivery attempt, when available.</summary>
    public required DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>Gets the time of the next scheduled attempt, when available.</summary>
    public required DateTimeOffset? NextAttemptAt { get; init; }

    /// <summary>Gets the time an inbound row entered terminal failure, when applicable.</summary>
    public required DateTimeOffset? TerminalAt { get; init; }
}
