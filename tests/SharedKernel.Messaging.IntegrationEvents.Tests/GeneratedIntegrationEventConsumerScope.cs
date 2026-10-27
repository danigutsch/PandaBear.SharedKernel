using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// A dependency injection scope that exposes the event envelope publisher for one simulated delivery.
/// </summary>
internal sealed class GeneratedIntegrationEventConsumerScope : IDisposable
{
    private readonly IServiceScope scope;

    /// <summary>
    /// Creates a scope from the service provider and resolves the event envelope publisher, and throws ArgumentNullException when the provider is null.
    /// </summary>
    /// /// <param name="serviceProvider">The provider the scope is created from.</param>
    public GeneratedIntegrationEventConsumerScope(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        scope = serviceProvider.CreateScope();
        Publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
    }

    /// <summary>
    /// Gets the event envelope publisher resolved inside the scope.
    /// </summary>
    public IEventEnvelopePublisher Publisher
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        scope.Dispose();
    }
}
