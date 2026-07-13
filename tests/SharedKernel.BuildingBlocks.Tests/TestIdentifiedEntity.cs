namespace SharedKernel.BuildingBlocks.Tests;

public sealed class TestIdentifiedEntity(Guid id) : IIdentified<Guid>
{
    public Guid Id { get; } = id;
}
