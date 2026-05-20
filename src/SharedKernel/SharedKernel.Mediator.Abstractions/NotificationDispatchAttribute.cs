namespace SharedKernel.Mediator;

/// <summary>
/// Selects the generated dispatch strategy for a notification contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class NotificationDispatchAttribute(NotificationDispatchStrategy strategy) : Attribute
{
    /// <summary>
    /// Gets the generated dispatch strategy for the notification.
    /// </summary>
    public NotificationDispatchStrategy Strategy { get; } = strategy;
}
