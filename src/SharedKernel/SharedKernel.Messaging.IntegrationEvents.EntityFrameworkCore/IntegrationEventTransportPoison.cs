using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Inspects and releases transport messages that exhausted their consume attempts. Call it from an audited operator
/// workflow; it does not record who replayed a message.
/// </summary>
public static class IntegrationEventTransportPoison
{
    /// <summary>Counts poisoned messages for one consumer.</summary>
    /// <param name="dbContext">The context that maps the transport table.</param>
    /// <param name="consumerName">The consumer name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TContext">The transport context type.</typeparam>
    /// <returns>The number of poisoned messages.</returns>
    /// <exception cref="ArgumentException"><paramref name="consumerName"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public static Task<int> CountPoisoned<TContext>(TContext dbContext, string consumerName, CancellationToken ct)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        return dbContext.Set<IntegrationEventTransportMessage>()
            .CountAsync(message => message.ConsumerName == consumerName && message.PoisonedAt != null, ct);
    }

    /// <summary>
    /// Returns one poisoned message to ordinary delivery with a fresh attempt budget and saves the change.
    /// </summary>
    /// <param name="dbContext">The context that maps the transport table.</param>
    /// <param name="consumerName">The consumer name.</param>
    /// <param name="eventId">The envelope event identifier.</param>
    /// <param name="releasedAt">When the message becomes claimable again.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TContext">The transport context type.</typeparam>
    /// <returns><see langword="true" /> when a poisoned message was released.</returns>
    /// <exception cref="ArgumentException"><paramref name="consumerName"/> or <paramref name="eventId"/> is <see langword="null"/>, empty, or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public static async Task<bool> ReleasePoisoned<TContext>(
        TContext dbContext,
        string consumerName,
        string eventId,
        DateTimeOffset releasedAt,
        CancellationToken ct)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        var message = await dbContext.Set<IntegrationEventTransportMessage>()
            .SingleOrDefaultAsync(
                candidate => candidate.ConsumerName == consumerName
                             && candidate.EventId == eventId
                             && candidate.PoisonedAt != null,
                ct)
            .ConfigureAwait(false);
        if (message is null)
        {
            return false;
        }

        message.ReleaseFromPoison(releasedAt);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
