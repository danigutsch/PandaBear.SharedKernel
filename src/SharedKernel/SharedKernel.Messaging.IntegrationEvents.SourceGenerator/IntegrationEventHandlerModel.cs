namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

internal sealed record IntegrationEventHandlerModel(
    string IntegrationEventType,
    string HandlerType);
