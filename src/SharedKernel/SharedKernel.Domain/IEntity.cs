namespace SharedKernel.Domain;

/// <summary>
/// Marks a domain entity identified by a stable identifier.
/// </summary>
/// <typeparam name="TId">The entity identifier type.</typeparam>
public interface IEntity<out TId> : IIdentified<TId>;
