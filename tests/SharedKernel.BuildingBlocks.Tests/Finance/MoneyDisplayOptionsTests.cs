using System.Globalization;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests displaying money with options: the symbol before or after the amount, and whole amounts without zero cents.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ContactValueObjects")]
public sealed class MoneyDisplayOptionsTests
{
    private static readonly MoneyDisplayOptions AfterWithoutZeroCents = new() { OmitZeroMinorUnits = true, SymbolPlacement = MoneySymbolPlacement.After };

    [Theory]
    [InlineData("pt-BR", 2890, "2.890 €")]
    [InlineData("pt-BR", 2890.5, "2.890,50 €")]
    [InlineData("pt-BR", -2890, "-2.890 €")]
    [InlineData("en-US", 2890, "2,890€")]
    [InlineData("en-US", 2890.25, "2,890.25€")]
    public void The_symbol_goes_after_the_amount_and_zero_cents_are_left_out(string culture, double amount, string expected)
    {
        // Arrange
        var money = Money.Create((decimal)amount, Currency.Eur).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo(culture), AfterWithoutZeroCents);

        // Assert
        display.ShouldBe(expected);
    }

    [Theory]
    [InlineData("pt-BR", "€ 2.890,00")]
    [InlineData("en-US", "€2,890.00")]
    public void The_symbol_goes_before_the_amount_keeping_the_culture_spacing(string culture, string expected)
    {
        // Arrange
        var money = Money.Create(2890m, Currency.Eur).ShouldBeSuccess();
        var options = new MoneyDisplayOptions { SymbolPlacement = MoneySymbolPlacement.Before };

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo(culture), options);

        // Assert
        display.ShouldBe(expected);
    }

    [Fact]
    public void A_negative_amount_with_the_symbol_before_keeps_the_sign_first()
    {
        // Arrange
        var money = Money.Create(-2890m, Currency.Eur).ShouldBeSuccess();
        var options = new MoneyDisplayOptions { SymbolPlacement = MoneySymbolPlacement.Before, OmitZeroMinorUnits = true };

        // Act
        var pt = money.ToDisplayString(CultureInfo.GetCultureInfo("pt-BR"), options);
        var en = money.ToDisplayString(CultureInfo.GetCultureInfo("en-US"), options);

        // Assert
        pt.ShouldBe("-€ 2.890");
        en.ShouldBe("-€2,890");
    }

    [Fact]
    public void The_default_options_display_like_the_culture_format()
    {
        // Arrange
        var money = Money.Create(1234.56m, Currency.Brl).ShouldBeSuccess();
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        // Act
        var display = money.ToDisplayString(culture, new MoneyDisplayOptions());

        // Assert
        display.ShouldBe(money.ToDisplayString(culture));
        display.ShouldBe("R$ 1.234,56");
    }

    [Fact]
    public void Leaving_out_zero_cents_keeps_the_culture_placement()
    {
        // Arrange
        var money = Money.Create(2890m, Currency.Brl).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo("pt-BR"), new MoneyDisplayOptions { OmitZeroMinorUnits = true });

        // Assert
        display.ShouldBe("R$ 2.890");
    }

    [Fact]
    public void A_currency_without_minor_units_is_unchanged()
    {
        // Arrange
        var money = Money.Create(2890m, Currency.Create("JPY").ShouldBeSuccess()).ShouldBeSuccess();

        // Act
        var display = money.ToDisplayString(CultureInfo.GetCultureInfo("en-US"), AfterWithoutZeroCents);

        // Assert
        display.ShouldBe("2,890¥");
    }

    [Fact]
    public void Missing_arguments_and_an_undefined_placement_are_refused()
    {
        // Arrange
        var money = Money.Create(1m, Currency.Eur).ShouldBeSuccess();

        // Act
        Action noProvider = () => money.ToDisplayString(null!, AfterWithoutZeroCents);
        Action noOptions = () => money.ToDisplayString(CultureInfo.InvariantCulture, null!);
        Action undefined = () => _ = new MoneyDisplayOptions { SymbolPlacement = (MoneySymbolPlacement)7 };

        // Assert
        noProvider.ShouldThrow<ArgumentNullException>();
        noOptions.ShouldThrow<ArgumentNullException>();
        undefined.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
