using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PandaBear.Scheduling;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Background service that polls the transport consumer at the configured interval.
/// </summary>
/// <param name="consumer">Consumer that handles pending messages.</param>
/// <param name="logger">Logger for the polling loop.</param>
/// <param name="options">Monitor that supplies the per-context consumer options.</param>
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
