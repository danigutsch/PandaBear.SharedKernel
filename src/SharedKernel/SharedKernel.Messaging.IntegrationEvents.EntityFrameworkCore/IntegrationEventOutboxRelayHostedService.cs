using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PandaBear.Scheduling;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Background service that polls the outbox relay at the configured interval.
/// </summary>
/// <param name="relay">Relay that publishes pending messages.</param>
/// <param name="logger">Logger for the polling loop.</param>
/// <param name="options">Monitor that supplies the per-context relay options.</param>
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
