using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Scoped outbox relay provider backed by a recording publisher and the system clock, disposed together.
/// </summary>
internal sealed class ConsumerMessagingTestScope : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;

    /// <summary>
    /// Builds the outbox relay provider and opens an async scope on it.
    /// </summary>
    public ConsumerMessagingTestScope()
    {
        provider = ConsumerMessagingTestServices.CreateOutboxRelayProvider(new RecordingEventEnvelopePublisher(), TimeProvider.System);
        scope = provider.CreateAsyncScope();
    }

    /// <summary>
    /// ConsumerOutboxDbContext resolved from the scope.
    /// </summary>
    public ConsumerOutboxDbContext DbContext => scope.ServiceProvider.GetRequiredService<ConsumerOutboxDbContext>();

    /// <summary>
    /// Disposes the scope and then the provider.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }
}
