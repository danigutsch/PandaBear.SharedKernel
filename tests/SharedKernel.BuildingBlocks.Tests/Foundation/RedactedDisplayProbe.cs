namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>A record with nested, optional and newly added members.</summary>
/// <param name="Input">The nested value supplied to the test record.</param>
internal sealed record RedactedDisplayProbe(RedactedDisplayInput? Input) : RedactedDisplayRecord
{
    /// <summary>Gets the nested input when it was initialized.</summary>
    /// <exception cref="InvalidOperationException">The nested input was not initialized.</exception>
    public RedactedDisplayInput RequiredInput => Input ?? throw new InvalidOperationException();

    /// <summary>Gets an added synthetic member.</summary>
    public string AddedMember { get; init; } = string.Empty;
}
