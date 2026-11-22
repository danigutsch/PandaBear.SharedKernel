using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Seeds and reads a fixture-owned database with custom outbox and transport mappings.</summary>
/// <param name="database">The isolated test database.</param>
/// <param name="dataSource">The data source owned by the scenario.</param>
/// <param name="provider">The configured EF Core services.</param>
/// <param name="commands">The PostgreSQL commands recorded by EF Core.</param>
internal sealed class PostgreSqlMessagingHealthScenario(
    PostgreSqlTestDatabaseLease database, NpgsqlDataSource dataSource, ServiceProvider provider, List<string> commands) : IAsyncDisposable
{
    /// <summary>Gets the commands executed for the most recent read.</summary>
    internal IReadOnlyList<string> ReadCommands => commands;

    /// <summary>Creates and initializes a fresh, isolated database.</summary>
    internal static async Task<PostgreSqlMessagingHealthScenario> Create(PostgreSqlFixture fixture, CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;
        try
        {
            dataSource = database.CreateDataSource(nameof(PostgreSqlMessagingHealthScenario));
            var services = new ServiceCollection();
            services.AddIntegrationEventOutbox<ConsumerTransportDbContext>(options =>
            {
                options.OutboxSchema = "health_outbox";
                options.OutboxTableName = "operations_outbox";
                options.TransportSchema = "health_transport";
                options.TransportTableName = "operations_transport";
            });
            services.AddIntegrationEventTransportStorage<ConsumerTransportDbContext>();
            var commands = new List<string>();
            services.AddDbContext<ConsumerTransportDbContext>(options => options.UseNpgsql(dataSource)
                .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
            provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            return new PostgreSqlMessagingHealthScenario(database, dataSource, provider, commands);
        }
        catch (Exception failure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(failure, provider, dataSource, database);
            throw;
        }
    }

    /// <summary>Stores a new outbox message after applying any requested test transition.</summary>
    internal async Task SeedOutbox(DateTimeOffset enqueuedAt, CancellationToken ct,
        Action<IntegrationEventOutboxMessage>? transition = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(Guid.CreateVersion7(), enqueuedAt);
        var message = IntegrationEventOutboxMessageFactory.Create(
            integrationEvent, new ComposedMessagingTestIntegrationEventSerializer(), enqueuedAt);
        transition?.Invoke(message);
        context.Set<IntegrationEventOutboxMessage>().Add(message);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Stores a consumer's transport message after applying any requested test transition.</summary>
    internal async Task SeedTransport(string consumer, DateTimeOffset receivedAt, CancellationToken ct,
        Action<IntegrationEventTransportMessage>? transition = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var id = Guid.CreateVersion7();
        var message = new IntegrationEventTransportMessage(id, consumer, TransportEnvelopeFactory.Create(id.ToString("D")), receivedAt);
        transition?.Invoke(message);
        context.Set<IntegrationEventTransportMessage>().Add(message);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Reads the outbox aggregate from a fresh scope.</summary>
    internal async Task<IntegrationEventOutboxHealth> ReadOutbox(DateTimeOffset cutoff, CancellationToken ct)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventMessagingHealth.ReadOutbox(context, cutoff, ct);
    }

    /// <summary>Reads the scoped transport aggregate from a fresh scope.</summary>
    internal async Task<IntegrationEventTransportHealth> ReadTransport(string consumer, DateTimeOffset cutoff, CancellationToken ct)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventMessagingHealth.ReadTransport(context, consumer, cutoff, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);
}
