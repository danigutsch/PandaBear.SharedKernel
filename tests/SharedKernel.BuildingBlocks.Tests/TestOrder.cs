
namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of a price and an address, used to test equality over nested value objects.
/// </summary>
/// <param name="price">The price component.</param>
/// <param name="address">The address component.</param>
internal sealed class TestOrder(TestMoney price, TestAddress address) : ValueObject
{
    /// <summary>
    /// Gets the price.
    /// </summary>
    public TestMoney Price { get; } = price;

    /// <summary>
    /// Gets the delivery address.
    /// </summary>
    public TestAddress Address { get; } = address;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Price;
        yield return Address;
    }
}
