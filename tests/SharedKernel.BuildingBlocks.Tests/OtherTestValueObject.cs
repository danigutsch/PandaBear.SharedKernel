namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class OtherTestValueObject(string city, int nights) : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return city;
        yield return nights;
    }
}
