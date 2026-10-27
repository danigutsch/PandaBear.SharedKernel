namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// An entity identified by a Guid, used in Option conversion tests.
/// </summary>
/// /// <param name="id">The identifier of the entity.</param>
public sealed class TestIdentifiedEntity(Guid id) : IIdentified<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; } = id;
}
