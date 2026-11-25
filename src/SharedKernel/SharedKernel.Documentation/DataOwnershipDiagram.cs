namespace SharedKernel.Documentation;

/// <summary>The caller-owned trust boundaries, nodes, and access relationships of a data ownership diagram.</summary>
/// <param name="boundaries">The declared trust boundaries.</param>
/// <param name="services">The consuming services.</param>
/// <param name="stores">The accessed data stores.</param>
/// <param name="accesses">The service-to-store relationships.</param>
public sealed class DataOwnershipDiagram(
    IReadOnlyList<DataOwnershipBoundary> boundaries,
    IReadOnlyList<DataOwnershipNode> services,
    IReadOnlyList<DataOwnershipNode> stores,
    IReadOnlyList<DataOwnershipAccess> accesses)
{
    /// <summary>Gets the declared trust boundaries.</summary>
    public IReadOnlyList<DataOwnershipBoundary> Boundaries { get; } = boundaries;

    /// <summary>Gets the consuming services.</summary>
    public IReadOnlyList<DataOwnershipNode> Services { get; } = services;

    /// <summary>Gets the accessed data stores.</summary>
    public IReadOnlyList<DataOwnershipNode> Stores { get; } = stores;

    /// <summary>Gets the service-to-store relationships.</summary>
    public IReadOnlyList<DataOwnershipAccess> Accesses { get; } = accesses;
}
