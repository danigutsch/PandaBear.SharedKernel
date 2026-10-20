using Microsoft.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

internal interface IIntegrationEventOutboxClaimStrategy<TContext>
    where TContext : DbContext
{
    ValueTask<IntegrationEventOutboxMessage[]> ClaimPending(
        TContext dbContext,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct);
}
