
namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class TestAddress(string street, string? city) : ValueObject
{
    public string Street { get; } = street;

    public string? City { get; } = city;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
    }
}
