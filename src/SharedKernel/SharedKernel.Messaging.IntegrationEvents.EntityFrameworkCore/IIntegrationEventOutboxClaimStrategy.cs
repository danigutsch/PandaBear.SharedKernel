using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Claims due outbox messages for one relay so concurrent relays do not publish the same message.</summary>
internal interface IIntegrationEventOutboxClaimStrategy<TContext>
    where TContext : DbContext
{
    /// <summary>Claims up to <paramref name="batchSize" /> unpublished, due, unclaimed messages for <paramref name="claimedBy" /> until <paramref name="claimedUntil" /> and returns them.</summary>
    ValueTask<IntegrationEventOutboxMessage[]> ClaimPending(
        TContext dbContext,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct);
}
