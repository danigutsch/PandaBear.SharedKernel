
namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of a street and an optional city, used in value object equality tests.
/// </summary>
/// <param name="street">The street component.</param>
/// <param name="city">The optional city component.</param>
internal sealed class TestAddress(string street, string? city) : ValueObject
{
    /// <summary>
    /// Gets the street.
    /// </summary>
    public string Street { get; } = street;

    /// <summary>
    /// Gets the optional city.
    /// </summary>
    public string? City { get; } = city;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
    }
}
