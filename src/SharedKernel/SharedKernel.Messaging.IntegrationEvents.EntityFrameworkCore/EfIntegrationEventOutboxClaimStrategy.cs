using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Provider-neutral claim strategy: loads due messages with a query, marks them claimed, and saves, relying on concurrency tokens to detect competing claimers.</summary>
internal sealed class EfIntegrationEventOutboxClaimStrategy<TContext>
    : IIntegrationEventOutboxClaimStrategy<TContext>
    where TContext : DbContext
{
    /// <inheritdoc />
    public async ValueTask<IntegrationEventOutboxMessage[]> ClaimPending(
        TContext dbContext,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct)
    {
        var messages = await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(message => message.PublishedAt == null
                && (message.NextPublishAttemptAt == null || message.NextPublishAttemptAt <= now)
                && (message.ClaimedUntil == null || message.ClaimedUntil <= now))
            .OrderBy(message => message.EnqueuedAt)
            .Take(batchSize)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        if (messages.Length == 0)
        {
            return [];
        }

        foreach (var message in messages)
        {
            message.MarkClaimed(claimedBy, claimedUntil);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return [];
        }

        return messages;
    }
}
