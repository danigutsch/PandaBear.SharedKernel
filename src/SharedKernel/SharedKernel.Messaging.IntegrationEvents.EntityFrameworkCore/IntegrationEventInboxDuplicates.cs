using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Inspects explicitly opted-in completed inbox duplicates in PostgreSQL.</summary>
/// <remarks>These operations do not claim, restart or complete an inbox entry. The application owns access and retention.</remarks>
public static class IntegrationEventInboxDuplicates
{
    private const int MaximumPageSize = 100;

    /// <summary>Reads completed duplicate metadata for one consumer in descending time and event identifier order.</summary>
    /// <param name="dbContext">A context with the opted-in inbox mapping.</param>
    /// <param name="consumerName">The exact consumer to inspect.</param>
    /// <param name="eventId">The event to inspect, or <see langword="null"/> to include every event owned by the consumer.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The current page of metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="consumerName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the consumer name is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static Task<IntegrationEventInboxDuplicatePage> ReadPage(
        DbContext dbContext, string consumerName, Guid? eventId, int limit, IntegrationEventInboxDuplicateCursor? cursor,
        CancellationToken ct) => ReadPage(dbContext, consumerName, eventId, observedSince: null, limit, cursor, ct);

    /// <summary>Reads completed duplicate metadata for one consumer in descending time and event identifier order.</summary>
    /// <param name="dbContext">A context with the opted-in inbox mapping.</param>
    /// <param name="consumerName">The exact consumer to inspect.</param>
    /// <param name="eventId">The event to inspect, or <see langword="null"/> to include every event owned by the consumer.</param>
    /// <param name="observedSince">The inclusive lower bound for the latest observation time, or <see langword="null"/> for no bound.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation, or <see langword="null"/> for the first page. Keep the filters when continuing.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The current page of metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="consumerName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the consumer name is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static async Task<IntegrationEventInboxDuplicatePage> ReadPage(
        DbContext dbContext, string consumerName, Guid? eventId, DateTimeOffset? observedSince, int limit,
        IntegrationEventInboxDuplicateCursor? cursor,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaximumPageSize);
        var query = ForConsumer(dbContext, consumerName);
        if (eventId is { } id)
        {
            query = query.Where(row => row.EventId == id);
        }

        if (observedSince is { } since)
        {
            var sinceUtc = since.ToUniversalTime();
            query = query.Where(row => row.LastObservedAt >= sinceUtc);
        }

        if (cursor is { } position)
        {
            var observedAt = position.LastObservedAt.ToUniversalTime();
            query = query.Where(row => row.LastObservedAt < observedAt
                || (row.LastObservedAt == observedAt && row.EventId.CompareTo(position.EventId) < 0));
        }

        var rows = await query.OrderByDescending(row => row.LastObservedAt)
            .ThenByDescending(row => row.EventId)
            .Select(row => new IntegrationEventInboxDuplicateRow(row.EventId, row.DuplicateCount, row.LastObservedAt))
            .Take(limit + 1)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        IntegrationEventInboxDuplicateCursor? next = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(limit);
            var last = rows[^1];
            next = new IntegrationEventInboxDuplicateCursor(last.LastObservedAt, last.EventId);
        }

        return new IntegrationEventInboxDuplicatePage(rows.AsReadOnly(), next);
    }

    /// <summary>Reads retained completed duplicate counts for one consumer without loading entries.</summary>
    /// <param name="dbContext">A context with the opted-in inbox mapping.</param>
    /// <param name="consumerName">The exact consumer to inspect.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The retained counts and latest observation time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="consumerName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the consumer name is invalid.</exception>
    public static async Task<IntegrationEventInboxDuplicateHealth> ReadHealth(
        DbContext dbContext, string consumerName, CancellationToken ct)
    {
        var result = await ForConsumer(dbContext, consumerName).GroupBy(static _ => 1)
            .Select(rows => new
            {
                Entries = rows.LongCount(),
                Duplicates = rows.Sum(row => (decimal)row.DuplicateCount),
                Latest = rows.Max(row => row.LastObservedAt),
            })
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return result is null ? default : new(result.Entries, (long)Math.Min(result.Duplicates, long.MaxValue), result.Latest);
    }

    private static IQueryable<IntegrationEventInboxDuplicateObservation> ForConsumer(DbContext dbContext, string consumerName)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ValidateConsumer(consumerName);
        return dbContext.Set<IntegrationEventInboxDuplicateObservation>().Where(row => row.ConsumerName == consumerName);
    }

    /// <summary>Validates an exact consumer identifier shared by registration and inspection.</summary>
    /// <exception cref="ArgumentException">The name is empty, blank or too long.</exception>
    internal static void ValidateConsumer(string consumerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        if (consumerName.Length > IntegrationEventTransportMessage.ConsumerNameMaxLength)
        {
            throw new ArgumentException("The consumer name exceeds the storage limit.", nameof(consumerName));
        }
    }
}
