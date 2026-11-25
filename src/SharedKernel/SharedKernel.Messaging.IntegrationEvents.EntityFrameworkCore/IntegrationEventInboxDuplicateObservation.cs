namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Retains a bounded observation for a completed inbox operation.</summary>
internal sealed class IntegrationEventInboxDuplicateObservation
{
    /// <summary>Gets the parent operation scope.</summary>
    public required string Scope { get; init; }

    /// <summary>Gets the parent operation key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the consumer that owns the observation.</summary>
    public required string ConsumerName { get; init; }

    /// <summary>Gets the immutable integration event identifier.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the number of completed duplicate deliveries observed.</summary>
    public int DuplicateCount { get; init; }

    /// <summary>Gets the latest time at which a completed duplicate was observed.</summary>
    public DateTimeOffset LastObservedAt { get; init; }
}
