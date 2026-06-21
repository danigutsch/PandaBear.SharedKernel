namespace SharedKernel.EventSourcing;

/// <summary>
/// Defines storage-neutral projection checkpoint operations.
/// </summary>
public interface IProjectionCheckpointStore
{
    /// <summary>
    /// Gets a projection checkpoint by projection name.
    /// </summary>
    ValueTask<ProjectionCheckpoint?> Get(string projectionName, CancellationToken ct);

    /// <summary>
    /// Saves a projection checkpoint.
    /// </summary>
    ValueTask Save(ProjectionCheckpoint checkpoint, CancellationToken ct);
}
