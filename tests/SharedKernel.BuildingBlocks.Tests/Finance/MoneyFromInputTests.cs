using System.Globalization;
using System.Text.Json;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>
/// Tests creating money from an amount a person typed, whose written decimal places, trailing zeros included, must fit the currency's
/// minor unit.
/// </summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class MoneyFromInputTests
{
    [Theory]
    [InlineData("EUR", "1500.50", "EUR 1500.50")]
    [InlineData("EUR", "1.50", "EUR 1.50")]
    [InlineData("EUR", "1500.5", "EUR 1500.50")]
    [InlineData("EUR", "1500", "EUR 1500.00")]
    [InlineData("BRL", "0.01", "BRL 0.01")]
    [InlineData("JPY", "1500", "JPY 1500")]
    [InlineData("KWD", "1.500", "KWD 1.500")]
    [InlineData("KWD", "1500.25", "KWD 1500.250")]
    [InlineData("CLF", "1.2345", "CLF 1.2345")]
    public void An_amount_written_with_at_most_the_currencys_decimal_places_is_accepted(string code, string typed, string expected)
    {
        // Arrange
        var currency = Currency.Create(code).ShouldBeSuccess();
        var amount = decimal.Parse(typed, CultureInfo.InvariantCulture);

        // Act
        var result = Money.CreateFromInput(amount, currency);

        // Assert
        var money = result.ShouldBeSuccess();
        money.Amount.ShouldBe(amount, "an accepted amount is kept as typed, never rounded");
        money.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("EUR", "1.500")]
    [InlineData("EUR", "1500.000")]
    [InlineData("BRL", "0.100")]
    [InlineData("USD", "1500.005")]
    [InlineData("JPY", "1500.0")]
    [InlineData("JPY", "1500.5")]
    [InlineData("KWD", "1.5000")]
    [InlineData("CLF", "1.23450")]
    public void An_amount_written_with_more_decimal_places_than_the_currency_is_refused_even_when_they_are_zeros(string code, string typed)
    {
        // Arrange
        var currency = Currency.Create(code).ShouldBeSuccess();
        var amount = decimal.Parse(typed, CultureInfo.InvariantCulture);

        // Act
        var result = Money.CreateFromInput(amount, currency);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid, "a decimal place the currency does not have is refused, not rounded or dropped");
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys.ToArray().ShouldBe(["amount"]);
    }

    [Theory]
    [InlineData("JPY", "1500.5", "Amount must be a whole number, like 1500.")]
    [InlineData("EUR", "1.500", "Amount must have at most 2 decimal places, like 1500 or 1500.50.")]
    [InlineData("KWD", "1.5000", "Amount must have at most 3 decimal places, like 1500 or 1500.500.")]
    [InlineData("CLF", "1.23450", "Amount must have at most 4 decimal places, like 1500 or 1500.5000.")]
    public void The_message_gives_the_currencys_decimal_places_with_examples_that_have_them(string code, string typed, string message)
    {
        // Arrange
        var currency = Currency.Create(code).ShouldBeSuccess();
        var amount = decimal.Parse(typed, CultureInfo.InvariantCulture);

        // Act
        var result = Money.CreateFromInput(amount, currency);

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe(message);
        error.ValidationErrors.ShouldNotBeNull()["amount"].ShouldBe([message]);
    }

    [Theory]
    [InlineData("EUR", "1.500", "Base price must have at most 2 decimal places, like 1500 or 1500.50.")]
    [InlineData("JPY", "1500.0", "Base price must be a whole number, like 1500.")]
    public void The_field_and_label_of_the_error_can_be_chosen(string code, string typed, string message)
    {
        // Arrange
        var currency = Currency.Create(code).ShouldBeSuccess();
        var amount = decimal.Parse(typed, CultureInfo.InvariantCulture);

        // Act
        var result = Money.CreateFromInput(amount, currency, field: "basePrice", label: "Base price");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ShouldHaveSingleItem().Key.ShouldBe("basePrice");
        result.ErrorDetails.ShouldHaveValidationError("basePrice", [message], "the label starts the message and the field keys it");
    }

    [Fact]
    public void A_JSON_number_keeps_the_decimal_places_it_was_written_with()
    {
        // Arrange
        var reader = new Utf8JsonReader("1.500"u8);
        reader.Read();
        var amount = reader.GetDecimal();

        // Act
        var result = Money.CreateFromInput(amount, Currency.Eur);

        // Assert
        amount.Scale.ShouldBe(3, "System.Text.Json keeps the scale the number was written with");
        result.Status.ShouldBe(ResultStatus.Invalid, "1.500 in a request body is refused like 1.500 typed into a form");
    }

    [Fact]
    public void A_missing_currency_is_refused_on_the_currency_field()
    {
        // Arrange
        var amount = decimal.Parse("1.500", CultureInfo.InvariantCulture);

        // Act
        var result = Money.CreateFromInput(amount, null, field: "basePrice", label: "Base price");

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.ErrorDetails.ShouldHaveValidationError("currency", ["Currency is required."], "a missing currency is reported as by CreateExact");
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys.ToArray().ShouldBe(["currency"]);
    }
}
