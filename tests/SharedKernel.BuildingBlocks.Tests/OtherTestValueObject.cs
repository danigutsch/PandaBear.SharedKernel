namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object with the same shape as TestValueObject but a different type, used to check that equality is type-sensitive.
/// </summary>
/// /// <param name="city">The city component.</param>
/// /// <param name="nights">The number of nights component.</param>
internal sealed class OtherTestValueObject(string city, int nights) : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return city;
        yield return nights;
    }
}
