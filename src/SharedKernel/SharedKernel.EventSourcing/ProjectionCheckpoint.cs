namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents a projection checkpoint.
/// </summary>
/// <param name="ProjectionName">The projection name.</param>
/// <param name="Position">The last processed global position.</param>
public sealed record ProjectionCheckpoint(string ProjectionName, long Position);
