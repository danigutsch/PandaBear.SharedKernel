namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>The last position in a duplicate observation page. Keep the consumer and filter fixed across pages.</summary>
/// <param name="LastObservedAt">The most recent observation time of the last returned entry.</param>
/// <param name="EventId">The stable identifier used to order equal timestamps.</param>
public readonly record struct IntegrationEventInboxDuplicateCursor(DateTimeOffset LastObservedAt, Guid EventId);
