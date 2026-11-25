namespace SharedKernel.Documentation;

/// <summary>Describes an operation associated with an AsyncAPI message.</summary>
/// <param name="id">The operation key in the document.</param>
/// <param name="summary">The operation summary written to the document.</param>
public sealed class AsyncApiOperation(string id, string summary)
{
    /// <summary>Gets the operation key in the document.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the operation summary written to the document.</summary>
    public string Summary { get; } = summary;
}
