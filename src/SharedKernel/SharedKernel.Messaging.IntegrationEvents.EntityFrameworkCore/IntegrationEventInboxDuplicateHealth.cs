namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Completed duplicate observations for one consumer across retained inbox entries.</summary>
/// <param name="EntriesWithDuplicates">The number of inbox entries with a recorded duplicate.</param>
/// <param name="DuplicateCount">The sum of retained entry counters, capped at <see cref="long.MaxValue"/>.</param>
/// <param name="LastObservedAt">The latest observation, or <see langword="null"/> when there are none.</param>
public readonly record struct IntegrationEventInboxDuplicateHealth(
    long EntriesWithDuplicates, long DuplicateCount, DateTimeOffset? LastObservedAt);
