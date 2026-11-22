namespace PandaBear.Documentation;

/// <summary>A service or data store in a data ownership diagram.</summary>
/// <param name="id">The stable Mermaid node identifier.</param>
/// <param name="boundaryId">The node's trust boundary identifier.</param>
/// <param name="labelLines">The node label, in display order.</param>
public sealed class DataOwnershipNode(string id, string boundaryId, IReadOnlyList<string> labelLines)
{
    /// <summary>Gets the node's identifier.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the node's trust boundary identifier.</summary>
    public string BoundaryId { get; } = boundaryId;

    /// <summary>Gets the node's label lines.</summary>
    public IReadOnlyList<string> LabelLines { get; } = labelLines;
}
