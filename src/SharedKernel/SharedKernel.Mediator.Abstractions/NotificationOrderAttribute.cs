namespace SharedKernel.Mediator;

/// <summary>
/// Assigns an explicit sequential order to a notification handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NotificationOrderAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the explicit order for the notification handler.
    /// </summary>
    public int Order { get; } = order;
}
