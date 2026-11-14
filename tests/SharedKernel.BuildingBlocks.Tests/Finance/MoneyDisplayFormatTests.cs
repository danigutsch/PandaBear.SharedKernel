using System.Globalization;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests the culture-aware display of money: the currency's symbol, the culture's separators and symbol position, and the currency's minor
/// unit, next to the invariant <c>EUR 1.00</c> form that stays for logs and storage.
/// </summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class MoneyDisplayFormatTests
{
    [Theory]
    [InlineData("pt-BR", "BRL", "R$ 1.234,56")]
    [InlineData("pt-BR", "EUR", "€ 1.234,56")]
    [InlineData("pt-BR", "USD", "$ 1.234,56")]
    [InlineData("en-US", "USD", "$1,234.56")]
    [InlineData("en-US", "EUR", "€1,234.56")]
    [InlineData("en-US", "BRL", "R$1,234.56")]
    public void Money_is_displayed_with_the_currency_symbol_in_the_culture_format(string culture, string currency, string expected)
    {
        // Arrange
        var money = Money.Create(1234.56m, Currency.Create(currency).ShouldBeSuccess()).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo(culture));

        // Assert
        display.ShouldBe(expected);
    }

    [Fact]
    public void Portuguese_euro_puts_the_symbol_after_the_amount()
    {
        // Arrange
        var money = Money.Create(1234.56m, Currency.Eur).ShouldBeSuccess();
        var culture = CultureInfo.GetCultureInfo("pt-PT");

        // Act
        var display = money.ToDisplayString(culture);

        // Assert
        display.ShouldEndWith(" €", StringComparison.Ordinal);
        display.ShouldStartWith("1", StringComparison.Ordinal);
        display.ShouldContain("234,56", StringComparison.Ordinal);
    }

    [Fact]
    public void The_invariant_culture_uses_its_own_pattern_with_the_symbol()
    {
        // Arrange
        var money = Money.Create(1234.56m, Currency.Eur).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.InvariantCulture);

        // Assert
        display.ShouldBe("€1,234.56");
    }

    [Theory]
    [InlineData("JPY", "¥1,235")]
    [InlineData("KWD", "KWD1,234.560")]
    [InlineData("CHF", "CHF1,234.56")]
    public void The_minor_unit_decides_the_decimals_and_a_code_without_a_symbol_shows_the_code(string currency, string expected)
    {
        // Arrange
        var money = Money.Create(1234.56m, Currency.Create(currency).ShouldBeSuccess()).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo("en-US"));

        // Assert
        display.ShouldBe(expected);
    }

    [Fact]
    public void The_c_format_displays_and_the_general_format_stays_invariant()
    {
        // Arrange
        var money = Money.Create(1m, Currency.Eur).ShouldBeSuccess();
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        // Act
        var display = string.Format(culture, "{0:C}", money);
        var general = string.Format(culture, "{0}", money);

        // Assert
        display.ShouldBe("€ 1,00");
        general.ShouldBe("EUR 1.00");
        money.ToString().ShouldBe("EUR 1.00");
    }

    [Fact]
    public void An_unknown_format_is_refused()
    {
        // Arrange
        var money = Money.Create(1m, Currency.Eur).ShouldBeSuccess();

        // Act
        Action act = () => money.ToString("N", CultureInfo.InvariantCulture);

        // Assert
        act.ShouldThrow<FormatException>();
    }

    [Theory]
    [InlineData("BRL", "R$")]
    [InlineData("EUR", "€")]
    [InlineData("USD", "$")]
    [InlineData("GBP", "£")]
    [InlineData("JPY", "¥")]
    [InlineData("CHF", "CHF")]
    public void A_currency_has_a_symbol_or_its_code(string code, string symbol)
    {
        // Act
        var currency = Currency.Create(code).ShouldBeSuccess();

        // Assert
        currency.Symbol.ShouldBe(symbol);
    }
}
