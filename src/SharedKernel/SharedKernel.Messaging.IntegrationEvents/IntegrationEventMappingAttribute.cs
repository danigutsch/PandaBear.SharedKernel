namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Marks a static method that maps a domain event to one integration event.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IntegrationEventMappingAttribute : Attribute;
