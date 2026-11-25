using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>
/// Tests creation and validation of Portuguese tax identification numbers (NIF). Every number here is built from the check-digit
/// algorithm, not taken from a person or a company.
/// </summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "PaymentIdentifiers")]
public sealed class NifTests
{
    [Theory]
    [InlineData("123456789")]
    [InlineData("100000002")]
    [InlineData("200000004")]
    [InlineData("300000006")]
    [InlineData("450000001")]
    [InlineData("500000000")]
    [InlineData("600000001")]
    [InlineData("700000003")]
    [InlineData("800000005")]
    [InlineData("900000007")]
    [InlineData("999999990")]
    [InlineData("100000100")]
    public void Creates_a_nif_of_every_first_digit_with_its_check_digit(string value)
    {
        // Act
        var result = Nif.Create(value);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456788")]
    [InlineData("123456780")]
    [InlineData("500000001")]
    [InlineData("999999991")]
    [InlineData("450000000")]
    [InlineData("012345679")]
    [InlineData("000000000")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678a")]
    [InlineData("12345678９")]
    [InlineData("١٢٣٤٥٦٧٨٩")]
    [InlineData("123 456 789")]
    [InlineData(" 123456789")]
    [InlineData("123456789\n")]
    [InlineData("PT123456789")]
    public void Rejects_a_wrong_check_digit_a_leading_zero_and_noncanonical_text_without_echoing_it(string? value)
    {
        // Act
        var result = Nif.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe("NIF is not valid.");
        error.ValidationErrors.ShouldNotBeNull()["nif"].ShouldBe(["NIF is not valid."]);
    }

    [Theory]
    [InlineData("123 456 789", "123456789")]
    [InlineData("123.456.789", "123456789")]
    [InlineData("123-456-789", "123456789")]
    [InlineData("123–456—789", "123456789")]
    [InlineData("  500000000\t", "500000000")]
    [InlineData("12 34 56 78 9", "123456789")]
    public void Create_from_input_ignores_spaces_dots_and_dashes_anywhere(string input, string expected)
    {
        // Act
        var result = Nif.CreateFromInput(input);

        // Assert
        result.ShouldBeSuccess().ShouldBe(Nif.Create(expected).ShouldBeSuccess());
    }

    [Theory]
    [InlineData("123 456 788")]
    [InlineData("012 345 679")]
    [InlineData("123/456/789")]
    [InlineData("123,456,789")]
    [InlineData("PT 123 456 789")]
    public void Create_from_input_rejects_other_characters_and_wrong_digits(string input)
    {
        // Act
        var result = Nif.CreateFromInput(input);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["nif"].ShouldBe(["NIF is not valid."]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . - ")]
    public void Create_from_input_reports_a_missing_nif_as_required(string? input)
    {
        // Act
        var result = Nif.CreateFromInput(input);

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe("NIF is required.");
        error.ValidationErrors.ShouldNotBeNull()["nif"].ShouldBe(["NIF is required."]);
    }

    [Fact]
    public void Formatted_groups_the_digits_in_threes_while_to_string_stays_redacted()
    {
        // Arrange
        var nif = Nif.Create("123456789").ShouldBeSuccess();

        // Act
        var formatted = nif.Formatted;

        // Assert
        formatted.ShouldBe("123 456 789");
        nif.ToString().ShouldBe("Nif [redacted]");
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("12345678")]
    [InlineData("12345678a")]
    public void Formatted_returns_a_restored_value_unchanged_when_it_is_not_nine_digits(string value)
    {
        // Arrange
        var nif = Nif.Restore(value);

        // Act
        var formatted = nif.Formatted;

        // Assert
        formatted.ShouldBe(value);
    }

    [Fact]
    public void Restore_trims_and_keeps_a_stored_value_that_create_rejects()
    {
        // Act
        var restored = Nif.Restore(" 000000000 ");
        var valid = Nif.Restore("123456789");

        // Assert
        restored.Value.ShouldBe("000000000");
        Nif.Create("000000000").Status.ShouldBe(ResultStatus.Invalid);
        valid.ShouldBe(Nif.Create("123456789").ShouldBeSuccess());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Restore_refuses_a_blank_value(string value)
    {
        // Act
        var restore = () => Nif.Restore(value);

        // Assert
        restore.ShouldThrow<ArgumentException>().ParamName.ShouldBe("value");
    }

    [Fact]
    public void Restore_refuses_a_null_value()
    {
        // Act
        var restore = () => Nif.Restore(null!);

        // Assert
        restore.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("value");
    }
}
