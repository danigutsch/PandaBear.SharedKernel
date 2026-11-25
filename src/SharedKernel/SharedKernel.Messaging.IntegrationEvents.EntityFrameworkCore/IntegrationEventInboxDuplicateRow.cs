namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Metadata for a completed inbox operation with at least one observed duplicate.</summary>
/// <param name="EventId">The immutable integration event identifier.</param>
/// <param name="DuplicateCount">The number of recorded observations, capped at <see cref="int.MaxValue"/>.</param>
/// <param name="LastObservedAt">The latest duplicate observation time.</param>
public readonly record struct IntegrationEventInboxDuplicateRow(Guid EventId, int DuplicateCount, DateTimeOffset LastObservedAt);
