
namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class TestTwoStrings(string first, string second) : ValueObject
{
    public string First { get; } = first;

    public string Second { get; } = second;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Second;
    }
}
