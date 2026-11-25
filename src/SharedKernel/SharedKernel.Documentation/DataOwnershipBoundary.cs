namespace SharedKernel.Documentation;

/// <summary>A trust boundary in a data ownership diagram.</summary>
/// <param name="id">The stable Mermaid identifier suffix of the boundary.</param>
/// <param name="label">The boundary label.</param>
public sealed class DataOwnershipBoundary(string id, string label)
{
    /// <summary>Gets the boundary's identifier.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the boundary's label.</summary>
    public string Label { get; } = label;
}
