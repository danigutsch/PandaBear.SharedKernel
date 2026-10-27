
namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of an amount and a currency code, used in value object equality tests.
/// </summary>
/// /// <param name="amount">The monetary amount.</param>
/// /// <param name="currency">The currency code.</param>
internal sealed class TestMoney(decimal amount, string currency) : ValueObject
{
    /// <summary>
    /// Gets the monetary amount.
    /// </summary>
    public decimal Amount { get; } = amount;

    /// <summary>
    /// Gets the currency code.
    /// </summary>
    public string Currency { get; } = currency;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
