namespace SharedKernel.Domain;

/// <summary>
/// Exposes a stable identifier for a model.
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public interface IIdentified<out TId>
{
    /// <summary>
    /// Gets the stable identifier.
    /// </summary>
    TId Id { get; }
}
