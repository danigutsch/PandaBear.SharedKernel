using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Reads aggregate-only messaging health through the supplied context's relational mappings.
/// </summary>
/// <remarks>
/// Each read uses one SQL statement. A retry is due when its next retry timestamp is at or before the supplied cutoff.
/// A due count measures the retry timestamp and includes leased retries.
/// </remarks>
public static class IntegrationEventMessagingHealth
{
    /// <summary>Reads pending and retry data from the mapped outbox.</summary>
    /// <param name="dbContext">A context that maps the outbox table.</param>
    /// <param name="cutoff">The time at which to evaluate the due retry count.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The outbox aggregate, with zero counts and absent timestamps for an empty set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public static async Task<IntegrationEventOutboxHealth> ReadOutbox(
        DbContext dbContext, DateTimeOffset cutoff, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        cutoff = cutoff.ToUniversalTime();

        return await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(message => message.PublishedAt == null)
            .GroupBy(_ => 1)
            .Select(group => new IntegrationEventOutboxHealth(
                group.LongCount(),
                group.Min(message => (DateTimeOffset?)message.EnqueuedAt),
                group.LongCount(message => message.NextPublishAttemptAt != null),
                group.Min(message => message.NextPublishAttemptAt != null ? (DateTimeOffset?)message.EnqueuedAt : null),
                group.LongCount(message => message.NextPublishAttemptAt <= cutoff)))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>Reads terminal failures and retries addressed to one consumer in the mapped transport.</summary>
    /// <param name="dbContext">A context that maps the transport table.</param>
    /// <param name="consumerName">The exact consumer name to read.</param>
    /// <param name="cutoff">The time at which to evaluate the due retry count.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The consumer aggregate, with zero counts and absent timestamps for an empty set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="consumerName"/> is null, empty, or white-space.</exception>
    public static async Task<IntegrationEventTransportHealth> ReadTransport(
        DbContext dbContext, string consumerName, DateTimeOffset cutoff, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        cutoff = cutoff.ToUniversalTime();

        return await dbContext.Set<IntegrationEventTransportMessage>()
            .Where(message => message.ConsumerName == consumerName)
            .Select(message => new
            {
                message.PoisonedAt,
                message.ReceivedAt,
                RetryAt = message.ProcessedAt == null && message.PoisonedAt == null && message.ConsumeAttempts > 0
                    ? message.NextConsumeAttemptAt
                    : null,
            })
            .Where(message => message.PoisonedAt != null || message.RetryAt != null)
            .GroupBy(_ => 1)
            .Select(group => new IntegrationEventTransportHealth(
                group.LongCount(message => message.PoisonedAt != null),
                group.Min(message => message.PoisonedAt),
                group.LongCount(message => message.RetryAt != null),
                group.Min(message => message.RetryAt != null ? (DateTimeOffset?)message.ReceivedAt : null),
                group.LongCount(message => message.RetryAt <= cutoff)))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
