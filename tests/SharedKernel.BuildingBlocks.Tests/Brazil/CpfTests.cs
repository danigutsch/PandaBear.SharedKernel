using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests creation and validation of Cpf personal tax identifiers.
/// </summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class CpfTests
{
    [Theory]
    [InlineData("11122233396")]
    [InlineData("52998224725")]
    [InlineData("01234567890")]
    public void Creates_checksummed_cpf_and_preserves_leading_zeros(string value)
    {
        // Act
        var result = Cpf.Create(value);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("11122233300")]
    [InlineData("52998224724")]
    [InlineData("52998224715")]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    [InlineData("529.982.247-25")]
    [InlineData("5299822472")]
    [InlineData("529982247255")]
    [InlineData("5299822472５")]
    [InlineData("52998224725\n")]
    [InlineData(" 52998224725")]
    public void Rejects_invalid_or_noncanonical_cpf(string? value)
    {
        // Act
        var result = Cpf.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("52998224725", "529.982.247-25")]
    [InlineData("01234567890", "012.345.678-90")]
    public void Formatted_prints_the_display_mask_while_to_string_stays_redacted(string value, string expected)
    {
        // Arrange
        var cpf = Cpf.Create(value).ShouldBeSuccess();

        // Act
        var formatted = cpf.Formatted;

        // Assert
        formatted.ShouldBe(expected);
        cpf.ToString().ShouldBe("Cpf [redacted]");
        cpf.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("abcdefghijk")]
    [InlineData("1234567890a")]
    [InlineData("529.982.247-25")]
    public void Formatted_returns_a_restored_value_unchanged_when_it_is_not_eleven_digits(string value)
    {
        // Arrange
        var cpf = Cpf.Restore(value);

        // Act
        var formatted = cpf.Formatted;

        // Assert
        formatted.ShouldBe(value);
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("  012.345.678-90 ", "01234567890")]
    [InlineData("\t01234567890\n", "01234567890")]
    [InlineData("529982247-25", "52998224725")]
    [InlineData("529.982.24725", "52998224725")]
    [InlineData("529 982 247 25", "52998224725")]
    [InlineData("529-982-247.25", "52998224725")]
    [InlineData("529.982.247–25", "52998224725")]
    [InlineData("529 . 982 . 247 — 25", "52998224725")]
    public void Create_from_input_ignores_spaces_dots_and_dashes_anywhere(string input, string expected)
    {
        // Act
        var result = Cpf.CreateFromInput(input);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe(expected);
        result.Value.ShouldBe(Cpf.Create(expected).ShouldBeSuccess());
    }

    [Theory]
    [InlineData("529.982.247-2")]
    [InlineData("529.982.247-255")]
    [InlineData("529.982.247-24")]
    [InlineData("111.111.111-11")]
    [InlineData("529.98a.247-25")]
    [InlineData("529/982/247-25")]
    [InlineData("529,982,247-25")]
    [InlineData("５29.982.247-25")]
    public void Create_from_input_rejects_other_characters_and_wrong_digits_without_echoing_it(string input)
    {
        // Act
        var result = Cpf.CreateFromInput(input);

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe("CPF is not valid.");
        error.ValidationErrors.ShouldNotBeNull()["cpf"].ShouldBe(["CPF is not valid."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . - ")]
    public void Create_from_input_reports_a_missing_cpf_as_required(string? input)
    {
        // Act
        var result = Cpf.CreateFromInput(input);

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe("CPF is required.");
        error.ValidationErrors.ShouldNotBeNull()["cpf"].ShouldBe(["CPF is required."]);
    }
}
