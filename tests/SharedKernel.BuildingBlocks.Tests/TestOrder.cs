
namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class TestOrder(TestMoney price, TestAddress address) : ValueObject
{
    public TestMoney Price { get; } = price;

    public TestAddress Address { get; } = address;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Price;
        yield return Address;
    }
}
