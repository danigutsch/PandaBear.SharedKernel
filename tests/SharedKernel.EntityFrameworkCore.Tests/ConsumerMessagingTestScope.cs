using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class ConsumerMessagingTestScope : IAsyncDisposable
{
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;

    public ConsumerMessagingTestScope()
    {
        provider = ConsumerMessagingTestServices.CreateOutboxRelayProvider(new RecordingEventEnvelopePublisher(), TimeProvider.System);
        scope = provider.CreateAsyncScope();
    }

    public ConsumerOutboxDbContext DbContext => scope.ServiceProvider.GetRequiredService<ConsumerOutboxDbContext>();

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }
}
