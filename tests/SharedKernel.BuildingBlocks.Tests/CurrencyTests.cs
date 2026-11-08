using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the ISO 4217 allow-list and minor units of currencies.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class CurrencyTests
{
    [Theory]
    [InlineData("BIF")]
    [InlineData("CLP")]
    [InlineData("DJF")]
    [InlineData("GNF")]
    [InlineData("ISK")]
    [InlineData("JPY")]
    [InlineData("KMF")]
    [InlineData("KRW")]
    [InlineData("PYG")]
    [InlineData("RWF")]
    [InlineData("UGX")]
    [InlineData("UYI")]
    [InlineData("VND")]
    [InlineData("VUV")]
    [InlineData("XAF")]
    [InlineData("XOF")]
    [InlineData("XPF")]
    public void Zero_decimal_currencies_report_no_minor_units(string code)
    {
        // Act
        var currency = Currency.Create(code).ShouldBeSuccess();

        // Assert
        currency.MinorUnits.ShouldBe(0);
    }

    [Theory]
    [InlineData("BHD")]
    [InlineData("IQD")]
    [InlineData("JOD")]
    [InlineData("KWD")]
    [InlineData("LYD")]
    [InlineData("OMR")]
    [InlineData("TND")]
    public void Three_decimal_currencies_report_three_minor_units(string code)
    {
        // Act
        var currency = Currency.Create(code).ShouldBeSuccess();

        // Assert
        currency.MinorUnits.ShouldBe(3);
    }

    [Theory]
    [InlineData("CLF")]
    [InlineData("UYW")]
    public void Four_decimal_currencies_report_four_minor_units(string code)
    {
        // Act
        var currency = Currency.Create(code).ShouldBeSuccess();

        // Assert
        currency.MinorUnits.ShouldBe(4);
    }

    [Theory]
    [InlineData("BRL")]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("CHF")]
    [InlineData("GBP")]
    [InlineData("CNY")]
    [InlineData("ZWG")]
    [InlineData("XCG")]
    public void Other_currencies_report_two_minor_units(string code)
    {
        // Act
        var currency = Currency.Create(code).ShouldBeSuccess();

        // Assert
        currency.MinorUnits.ShouldBe(2);
    }

    [Theory]
    [InlineData("brl", "BRL")]
    [InlineData(" eur ", "EUR")]
    [InlineData("usd", "USD")]
    [InlineData("jpy", "JPY")]
    [InlineData("chf", "CHF")]
    [InlineData("gbp", "GBP")]
    [InlineData("kwd", "KWD")]
    [InlineData("clf", "CLF")]
    public void Known_currencies_are_accepted_and_upper_cased(string input, string expected)
    {
        // Act
        var currency = Currency.Create(input).ShouldBeSuccess();

        // Assert
        currency.Code.ShouldBe(expected);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("ABC")]
    [InlineData("XXX")]
    [InlineData("XAU")]
    [InlineData("XAG")]
    [InlineData("XPT")]
    [InlineData("XPD")]
    [InlineData("XTS")]
    [InlineData("XDR")]
    [InlineData("HRK")]
    [InlineData("BGN")]
    [InlineData("ZZZ")]
    public void Unknown_or_non_money_codes_are_rejected_without_echoing_the_input(string input)
    {
        // Act
        var result = Currency.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.Keys.ToArray().ShouldBe(["currency"]);
        errors["currency"].ShouldBe(["Currency is not a valid ISO 4217 currency code."]);
    }

    [Fact]
    public void Money_in_a_four_decimal_currency_keeps_four_decimals()
    {
        // Arrange
        var unidadDeFomento = Currency.Create("CLF").ShouldBeSuccess();

        // Act
        var accepted = Money.CreateExact(1.2345m, unidadDeFomento);
        var rejected = Money.CreateExact(1.23456m, unidadDeFomento);

        // Assert
        accepted.ShouldBeSuccess().Amount.ShouldBe(1.2345m);
        rejected.Status.ShouldBe(ResultStatus.Invalid);
    }
}
