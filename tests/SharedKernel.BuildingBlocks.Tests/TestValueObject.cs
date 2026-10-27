namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of a city and a number of nights, used in value object equality tests.
/// </summary>
/// /// <param name="city">The city component.</param>
/// /// <param name="nights">The number of nights component.</param>
internal sealed class TestValueObject(string city, int nights) : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return city;
        yield return nights;
    }
}
