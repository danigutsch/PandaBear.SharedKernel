namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

internal sealed record IntegrationEventRegistrationModel(
    string IntegrationEventType,
    bool IsConsumer,
    string? EventType);
