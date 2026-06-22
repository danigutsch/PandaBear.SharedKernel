namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class TestValueObject(string city, int nights) : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return city;
        yield return nights;
    }
}
