using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Inspects delivery metadata through existing EF Core mappings without claiming or changing rows.</summary>
/// <remarks>
/// Each read selects bounded delivery metadata in one statement. Applications own access control and the
/// handling of identifiers. Payloads, extension attributes, claim owners and stored error text stay in the database.
/// </remarks>
public static class IntegrationEventDeliveryInspection
{
    private const int MaximumPageSize = 100;

    /// <summary>Reads one retained outbox row by its exact event identifier, including after publication.</summary>
    /// <param name="dbContext">A context with the outbox storage mapping.</param>
    /// <param name="eventId">The exact envelope identifier stored by the producer.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Publication metadata, or <see langword="null"/> if the row is absent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="eventId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventId"/> is empty or blank.</exception>
    public static Task<IntegrationEventOutboxDelivery?> ReadRetainedOutbox(
        DbContext dbContext, string eventId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return dbContext.Set<IntegrationEventOutboxMessage>()
            .AsNoTracking()
            .Where(message => message.EventId == eventId)
            .Select(message => new IntegrationEventOutboxDelivery
            {
                StorageId = message.Id,
                EventId = message.EventId,
                EventType = message.EventType,
                RecordedAt = message.EnqueuedAt,
                PublishedAt = message.PublishedAt,
                FailedAttempts = message.PublishAttempts,
                LastAttemptAt = message.LastPublishAttemptAt,
                NextAttemptAt = message.NextPublishAttemptAt,
            })
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>Reads pending outbox messages, including scheduled retries.</summary>
    /// <param name="dbContext">A context with the outbox storage mapping.</param>
    /// <param name="filter">Optional filters on stored identifiers, type and recorded time.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page in descending order by timestamp and storage identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static Task<IntegrationEventDeliveryPage> ReadOutbox(
        DbContext dbContext, IntegrationEventDeliveryFilter filter, int limit, IntegrationEventDeliveryCursor? cursor, CancellationToken ct)
        => ReadOutboxPage(dbContext, filter, limit, cursor, matchingEventIds: null, ct);

    /// <summary>Reads pending outbox messages whose envelope identifiers match an application-owned query.</summary>
    /// <param name="dbContext">A context with the outbox storage mapping.</param>
    /// <param name="matchingEventIds">A query on the same context selecting the exact stored envelope identifiers.</param>
    /// <param name="filter">Optional filters on stored identifiers, type and recorded time.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation for the same filters, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching page in descending order by timestamp and storage identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="matchingEventIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static Task<IntegrationEventDeliveryPage> ReadMatchingOutbox(
        DbContext dbContext, IQueryable<string> matchingEventIds, IntegrationEventDeliveryFilter filter, int limit,
        IntegrationEventDeliveryCursor? cursor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(matchingEventIds);
        return ReadOutboxPage(dbContext, filter, limit, cursor, matchingEventIds, ct);
    }

    private static Task<IntegrationEventDeliveryPage> ReadOutboxPage(
        DbContext dbContext, IntegrationEventDeliveryFilter filter, int limit, IntegrationEventDeliveryCursor? cursor,
        IQueryable<string>? matchingEventIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var query = dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(message => message.PublishedAt == null)
            .Select(message => new IntegrationEventDeliveryEntry
            {
                StorageId = message.Id,
                EventId = message.EventId,
                EventType = message.EventType,
                RecordedAt = message.EnqueuedAt,
                Attempts = message.PublishAttempts,
                LastAttemptAt = message.LastPublishAttemptAt,
                NextAttemptAt = message.NextPublishAttemptAt,
                TerminalAt = null,
            });
        return ReadPage(query, filter, limit, cursor, matchingEventIds, ct);
    }

    /// <summary>Reads unprocessed transport messages for one consumer, including retries and terminal failures.</summary>
    /// <param name="dbContext">A context with the transport storage mapping.</param>
    /// <param name="consumerName">The exact consumer to inspect.</param>
    /// <param name="filter">Optional filters on stored identifiers, type and recorded time.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page in descending order by timestamp and storage identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="consumerName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="consumerName"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static Task<IntegrationEventDeliveryPage> ReadTransport(
        DbContext dbContext, string consumerName, IntegrationEventDeliveryFilter filter, int limit,
        IntegrationEventDeliveryCursor? cursor, CancellationToken ct)
        => ReadTransportPage(dbContext, consumerName, filter, limit, cursor, matchingEventIds: null, ct);

    /// <summary>Reads unfinished transport messages whose envelope identifiers match an application-owned query.</summary>
    /// <param name="dbContext">A context with the transport storage mapping.</param>
    /// <param name="consumerName">The exact consumer to inspect.</param>
    /// <param name="matchingEventIds">A query on the same context selecting the exact stored envelope identifiers.</param>
    /// <param name="filter">Optional filters on stored identifiers, type and recorded time.</param>
    /// <param name="limit">The number of rows to return, from one to 100.</param>
    /// <param name="cursor">A previous page's continuation for the same filters, or <see langword="null"/> for the first page.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching page in descending order by timestamp and storage identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/>, <paramref name="consumerName"/> or <paramref name="matchingEventIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="consumerName"/> is empty or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is outside the allowed range.</exception>
    public static Task<IntegrationEventDeliveryPage> ReadMatchingTransport(
        DbContext dbContext, string consumerName, IQueryable<string> matchingEventIds, IntegrationEventDeliveryFilter filter,
        int limit, IntegrationEventDeliveryCursor? cursor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(matchingEventIds);
        return ReadTransportPage(dbContext, consumerName, filter, limit, cursor, matchingEventIds, ct);
    }

    private static Task<IntegrationEventDeliveryPage> ReadTransportPage(
        DbContext dbContext, string consumerName, IntegrationEventDeliveryFilter filter, int limit,
        IntegrationEventDeliveryCursor? cursor, IQueryable<string>? matchingEventIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        var query = dbContext.Set<IntegrationEventTransportMessage>()
            .Where(message => message.ConsumerName == consumerName && message.ProcessedAt == null)
            .Select(message => new IntegrationEventDeliveryEntry
            {
                StorageId = message.Id,
                EventId = message.EventId,
                EventType = message.EventType,
                RecordedAt = message.ReceivedAt,
                Attempts = message.ConsumeAttempts,
                LastAttemptAt = message.LastConsumeAttemptAt,
                NextAttemptAt = message.NextConsumeAttemptAt,
                TerminalAt = message.PoisonedAt,
            });
        return ReadPage(query, filter, limit, cursor, matchingEventIds, ct);
    }

    private static async Task<IntegrationEventDeliveryPage> ReadPage(
        IQueryable<IntegrationEventDeliveryEntry> query, IntegrationEventDeliveryFilter filter, int limit,
        IntegrationEventDeliveryCursor? cursor, IQueryable<string>? matchingEventIds, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaximumPageSize);

        if (matchingEventIds is not null)
        {
            query = query.Where(entry => matchingEventIds.Contains(entry.EventId));
        }

        if (filter.EventId is { } eventId)
        {
            query = query.Where(entry => entry.EventId == eventId);
        }

        if (filter.EventType is { } eventType)
        {
            query = query.Where(entry => entry.EventType == eventType);
        }

        if (filter.RecordedSince is { } recordedSince)
        {
            var utc = recordedSince.ToUniversalTime();
            query = query.Where(entry => entry.RecordedAt >= utc);
        }

        if (cursor is { } position)
        {
            var recordedAt = position.RecordedAt.ToUniversalTime();
            query = query.Where(entry => entry.RecordedAt < recordedAt
                || (entry.RecordedAt == recordedAt && entry.StorageId.CompareTo(position.StorageId) < 0));
        }

        var rows = await query.OrderByDescending(entry => entry.RecordedAt)
            .ThenByDescending(entry => entry.StorageId)
            .Take(limit + 1)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        IntegrationEventDeliveryCursor? next = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(limit);
            var last = rows[^1];
            next = new IntegrationEventDeliveryCursor(last.RecordedAt, last.StorageId);
        }

        return new IntegrationEventDeliveryPage(rows.AsReadOnly(), next);
    }
}
