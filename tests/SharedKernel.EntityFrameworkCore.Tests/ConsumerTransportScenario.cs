using Microsoft.Extensions.DependencyInjection;
using PandaBear.Messaging;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Async scope over a transport test provider that exposes the context and publisher and disposes both with the provider.
/// </summary>
/// <param name="provider">The service provider the scenario scope is created from and disposed with.</param>
internal sealed class ConsumerTransportScenario(ServiceProvider provider) : IAsyncDisposable
{
    private readonly AsyncServiceScope scenarioScope = provider.CreateAsyncScope();

    /// <summary>
    /// ConsumerTransportDbContext resolved from the scenario scope.
    /// </summary>
    public ConsumerTransportDbContext DbContext => scenarioScope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();

    /// <summary>
    /// Event envelope publisher registered under the ConsumerTransportDbContext key.
    /// </summary>
    public IEventEnvelopePublisher Publisher =>
        scenarioScope.ServiceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(ConsumerTransportDbContext));

    /// <summary>
    /// Disposes the scenario scope and then the provider.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await scenarioScope.DisposeAsync();
        await provider.DisposeAsync();
    }
}
