namespace SharedKernel.Mediator;

/// <summary>
/// Represents the absence of a meaningful return value.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Gets the shared unit value.
    /// </summary>
    public static Unit Value => default;
}
