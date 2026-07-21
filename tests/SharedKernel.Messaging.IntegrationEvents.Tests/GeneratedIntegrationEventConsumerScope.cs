using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class GeneratedIntegrationEventConsumerScope : IDisposable
{
    private readonly IServiceScope scope;

    public GeneratedIntegrationEventConsumerScope(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        scope = serviceProvider.CreateScope();
        Publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        Handler = scope.ServiceProvider.GetRequiredService<CapturingIntegrationEventHandler>();
    }

    public IEventEnvelopePublisher Publisher { get; }

    public CapturingIntegrationEventHandler Handler { get; }

    public void Dispose()
    {
        scope.Dispose();
    }
}
