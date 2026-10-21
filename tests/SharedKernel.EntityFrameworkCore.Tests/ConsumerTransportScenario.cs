using Microsoft.Extensions.DependencyInjection;
using PandaBear.Messaging;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class ConsumerTransportScenario(ServiceProvider provider) : IAsyncDisposable
{
    private readonly AsyncServiceScope scenarioScope = provider.CreateAsyncScope();

    public ConsumerTransportDbContext DbContext => scenarioScope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();

    public IEventEnvelopePublisher Publisher =>
        scenarioScope.ServiceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(ConsumerTransportDbContext));

    public async ValueTask DisposeAsync()
    {
        await scenarioScope.DisposeAsync();
        await provider.DisposeAsync();
    }
}
