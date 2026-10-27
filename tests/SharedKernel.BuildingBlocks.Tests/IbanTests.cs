using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests creation and validation of Iban values.
/// </summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class IbanTests
{
    // SWIFT IBAN Registry release 103: one electronic-format example per registered country.
    [Theory]
    [MemberData(nameof(IbanTestCases.RegistryExamples), MemberType = typeof(IbanTestCases))]
    public void Creates_each_registered_country_example(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [MemberData(nameof(IbanTestCases.RegistryExamples), MemberType = typeof(IbanTestCases))]
    public void Rejects_changed_check_digits_for_each_registered_country(string value)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(value);
        var corrupted = value[..2] + (value[2] == '9' ? '0' : (char)(value[2] + 1)) + value[3..];

        // Act
        var result = Iban.Create(corrupted);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DE")]
    [InlineData("de89370400440532013000")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData(" DE89370400440532013000")]
    [InlineData("DE89370400440532013000\n")]
    [InlineData("DE８9370400440532013000")]
    [InlineData("DE8937040044053201300")]
    [InlineData("DE893704004405320130000")]
    public void Rejects_noncanonical_or_incomplete_candidates(string? value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("DE85A70400440532013000")]
    [InlineData("GB731EST12345698765432")]
    [InlineData("BR2999999A03A00010009795493C1")]
    [InlineData("FR462004A010050500013M02606")]
    [InlineData("ZZ22370400440532013000")]
    public void Rejects_country_or_structure_errors_even_with_recalculated_check_digits(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Rejects_oversized_candidates()
    {
        // Arrange
        var value = new string('A', 10_000);

        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData("DE00000000000000000066")]
    [InlineData("DE01000000000000000048")]
    [InlineData("DE99000000000000000030")]
    public void Rejects_noncanonical_check_digit_ranges_even_when_modulo_is_one(string value)
    {
        // Act
        var result = Iban.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }
}
