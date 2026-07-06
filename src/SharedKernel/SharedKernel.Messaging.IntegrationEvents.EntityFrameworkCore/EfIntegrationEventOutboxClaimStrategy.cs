using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class EfIntegrationEventOutboxClaimStrategy<TContext>
    : IIntegrationEventOutboxClaimStrategy<TContext>
    where TContext : DbContext
{
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
