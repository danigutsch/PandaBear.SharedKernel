using Microsoft.EntityFrameworkCore;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Claim strategy that serializes callers with a semaphore and hands out unpublished messages only until one claim has succeeded, so concurrent relays cannot publish the same message twice.
/// </summary>
internal sealed class SingleClaimIntegrationEventOutboxClaimStrategy<TContext> :
    IIntegrationEventOutboxClaimStrategy<TContext>,
    IDisposable
    where TContext : DbContext
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool claimed;

    /// <summary>
    /// Under the semaphore, returns no messages once a claim has succeeded; otherwise marks up to the batch size of due, unclaimed and unpublished messages as claimed and saves them.
    /// </summary>
    public async ValueTask<IntegrationEventOutboxMessage[]> ClaimPending(
        TContext dbContext,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (claimed)
            {
                return [];
            }

            var messages = await dbContext.Set<IntegrationEventOutboxMessage>()
                .Where(message => message.PublishedAt == null
                    && (message.NextPublishAttemptAt == null || message.NextPublishAttemptAt <= now)
                    && (message.ClaimedUntil == null || message.ClaimedUntil <= now))
                .OrderBy(message => message.EnqueuedAt)
                .Take(batchSize)
                .ToArrayAsync(ct)
                .ConfigureAwait(false);

            foreach (var message in messages)
            {
                message.MarkClaimed(claimedBy, claimedUntil);
            }

            _ = await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            claimed = messages.Length > 0;

            return messages;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        gate.Dispose();
    }
}
