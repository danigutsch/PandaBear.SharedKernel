namespace SharedKernel.EventSourcing;

/// <summary>
/// Defines a storage-neutral projection handler.
/// </summary>
public interface IProjection
{
    /// <summary>
    /// Gets the projection name used for checkpoint ownership.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Applies one event envelope to the projection.
    /// </summary>
    ValueTask Apply(EventEnvelope envelope, CancellationToken ct);
}
