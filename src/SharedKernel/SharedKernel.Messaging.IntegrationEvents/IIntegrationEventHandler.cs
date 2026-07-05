using SharedKernel.Mediator;

namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Handles a typed integration event.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type handled by the handler.</typeparam>
public interface IIntegrationEventHandler<in TIntegrationEvent> : INotificationHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent;
