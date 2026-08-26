using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Scheduling;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class IntegrationEventOutboxRelayHostedService<TContext>(
    EfIntegrationEventOutboxRelay<TContext> relay,
    ILogger<IntegrationEventOutboxRelayHostedService<TContext>> logger,
    IOptionsMonitor<IntegrationEventOutboxRelayOptions> options)
    : PollingBackgroundService(
        logger,
        $"integration-event-outbox-relay:{typeof(TContext).Name}",
        options.Get(IntegrationEventOptionsNames.Relay<TContext>()).PollInterval)
    where TContext : DbContext
{
    protected override async ValueTask<int> ExecuteBatch(CancellationToken stoppingToken) =>
        await relay.PublishPending(stoppingToken).ConfigureAwait(false);

}
