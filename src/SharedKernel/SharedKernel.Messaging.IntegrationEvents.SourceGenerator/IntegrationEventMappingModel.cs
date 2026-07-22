using Microsoft.CodeAnalysis;

namespace SharedKernel.Messaging.IntegrationEvents.SourceGenerator;

internal sealed class IntegrationEventMappingModel(
    bool isValid,
    string containingType,
    string methodName,
    Location? location,
    string domainEventType,
    string integrationEventType,
    string dispatchMethodName,
    string escapedMethodName)
{
    public bool IsValid { get; } = isValid;

    public string ContainingType { get; } = containingType;

    public string MethodName { get; } = methodName;

    public Location? Location { get; } = location;

    public string DomainEventType { get; } = domainEventType;

    public string IntegrationEventType { get; } = integrationEventType;

    public string DispatchMethodName { get; } = dispatchMethodName;

    public string EscapedMethodName { get; } = escapedMethodName;

    public static IntegrationEventMappingModel Invalid(string methodName, Location? location) =>
        new(false, string.Empty, methodName, location, string.Empty, string.Empty, string.Empty, string.Empty);
}
