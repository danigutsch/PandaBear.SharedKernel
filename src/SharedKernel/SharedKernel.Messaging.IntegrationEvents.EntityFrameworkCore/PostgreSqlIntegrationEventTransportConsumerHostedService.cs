using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PandaBear.Scheduling;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class PostgreSqlIntegrationEventTransportConsumerHostedService<TContext>(
    PostgreSqlIntegrationEventTransportConsumer<TContext> consumer,
    ILogger<PostgreSqlIntegrationEventTransportConsumerHostedService<TContext>> logger,
    IOptionsMonitor<IntegrationEventOutboxRelayOptions> options)
    : PollingBackgroundService(
        logger,
        $"integration-event-transport-consumer:{typeof(TContext).Name}",
        options.Get(IntegrationEventOptionsNames.Consumer<TContext>()).PollInterval)
    where TContext : DbContext
{
    protected override async ValueTask<int> ExecuteBatch(CancellationToken stoppingToken) =>
        await consumer.ConsumePending(stoppingToken).ConfigureAwait(false);

}
