using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests creating money that must already have the currency's precision.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ValueObjects")]
public sealed class MoneyExactTests
{
    [Theory]
    [InlineData("EUR", "100.50", "100.50")]
    [InlineData("EUR", "100", "100.00")]
    [InlineData("USD", "0.01", "0.01")]
    public void An_amount_within_the_currencys_precision_is_accepted(string code, string amount, string expected)
    {
        // Arrange
        var currency = Currency.Create(code).Value;

        // Act
        var money = Money.CreateExact(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), currency).ShouldBeSuccess();

        // Assert
        money.ToString().ShouldBe($"{code} {expected}");
    }

    [Theory]
    [InlineData("EUR", "100.005", "Amount cannot have more than 2 decimal places for EUR.")]
    [InlineData("BRL", "0.001", "Amount cannot have more than 2 decimal places for BRL.")]
    public void An_amount_with_more_decimals_than_the_currency_allows_is_rejected_not_rounded(string code, string amount, string message)
    {
        // Arrange
        var currency = Currency.Create(code).Value;

        // Act
        var result = Money.CreateExact(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), currency);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["amount"].ShouldBe([message]);
    }

    [Fact]
    public void The_field_and_label_of_the_error_can_be_chosen()
    {
        // Act
        var result = Money.CreateExact(1.234m, Currency.Eur, field: "basePrice", label: "Base price");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["basePrice"].ShouldBe(["Base price cannot have more than 2 decimal places for EUR."]);
    }

    [Fact]
    public void A_missing_currency_is_rejected()
    {
        // Act
        var result = Money.CreateExact(1m, null);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys.ToArray().ShouldBe(["currency"]);
    }
}
