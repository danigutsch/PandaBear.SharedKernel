namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

internal sealed record IntegrationEventMappingModel(
    string ContainingType,
    string MethodName,
    string DomainEventType,
    string IntegrationEventType,
    string DispatchMethodName,
    string EscapedMethodName);
