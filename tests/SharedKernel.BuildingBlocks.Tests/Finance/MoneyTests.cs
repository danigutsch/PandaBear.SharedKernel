using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests currency codes and money values.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class MoneyTests
{
    [Theory]
    [InlineData("BRL", "BRL", 2)]
    [InlineData(" eur ", "EUR", 2)]
    [InlineData("jpy", "JPY", 0)]
    [InlineData("KWD", "KWD", 3)]
    public void Currency_is_upper_cased_with_minor_units(string input, string code, int minorUnits)
    {
        // Act
        var currency = Currency.Create(input).ShouldBeSuccess();

        // Assert
        currency.Code.ShouldBe(code);
        currency.MinorUnits.ShouldBe(minorUnits);
        currency.ToString().ShouldBe(code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BR")]
    [InlineData("BRLL")]
    [InlineData("B1L")]
    public void Currency_rejects_invalid_codes(string? input)
    {
        // Act
        var result = Currency.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Well_known_currencies_are_available()
    {
        // Assert
        Currency.Brl.Code.ShouldBe("BRL");
        Currency.Eur.Code.ShouldBe("EUR");
        Currency.Usd.Code.ShouldBe("USD");
    }

    [Theory]
    [InlineData("10.005", 10.01)]
    [InlineData("10.004", 10.00)]
    [InlineData("-10.005", -10.01)]
    public void Money_rounds_away_from_zero_to_minor_units(string amount, double expected)
    {
        // Act
        var money = Money.Create(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Currency.Brl).ShouldBeSuccess();

        // Assert
        money.Amount.ShouldBe((decimal)expected);
    }

    [Fact]
    public void Money_in_zero_decimal_currency_rounds_to_whole_units()
    {
        // Arrange
        var yen = Currency.Create("JPY").ShouldBeSuccess();

        // Act
        var money = Money.Create(100.5m, yen).ShouldBeSuccess();

        // Assert
        money.Amount.ShouldBe(101m);
        money.ToString().ShouldBe("JPY 101");
    }

    [Fact]
    public void Money_requires_a_currency()
    {
        // Act
        var result = Money.Create(1m, null);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Money_reports_positivity_and_formats_invariantly()
    {
        // Act
        var positive = Money.Create(1234.5m, Currency.Usd).ShouldBeSuccess();
        var zero = Money.Create(0m, Currency.Usd).ShouldBeSuccess();

        // Assert
        positive.IsPositive.ShouldBeTrue();
        zero.IsPositive.ShouldBeFalse();
        positive.ToString().ShouldBe("USD 1234.50");
    }

    [Fact]
    public void Money_adds_only_the_same_currency()
    {
        // Arrange
        var ten = Money.Create(10m, Currency.Eur).ShouldBeSuccess();
        var five = Money.Create(5m, Currency.Eur).ShouldBeSuccess();
        var dollars = Money.Create(5m, Currency.Usd).ShouldBeSuccess();

        // Act & Assert
        ten.Add(five).ShouldBeSuccess().Amount.ShouldBe(15m);
        ten.Add(dollars).Status.ShouldBe(ResultStatus.Invalid);
        Action addNull = () => ten.Add(null!);
        addNull.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Money_with_equal_amount_and_currency_is_equal()
    {
        // Act
        var first = Money.Create(10m, Currency.Brl).ShouldBeSuccess();
        var second = Money.Create(10.00m, Currency.Create("brl").ShouldBeSuccess()).ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
    }
}
