
namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of two strings, used to test that equality depends on component order.
/// </summary>
/// <param name="first">The first string component.</param>
/// <param name="second">The second string component.</param>
internal sealed class TestTwoStrings(string first, string second) : ValueObject
{
    /// <summary>
    /// Gets the first string.
    /// </summary>
    public string First { get; } = first;

    /// <summary>
    /// Gets the second string.
    /// </summary>
    public string Second { get; } = second;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Second;
    }
}
