namespace SharedKernel.Testing;

/// <summary>
/// Documents why an xUnit collection must disable parallel execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SerialTestJustificationAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets the reason serial execution is required.
    /// </summary>
    public string Reason { get; } = reason;
}
