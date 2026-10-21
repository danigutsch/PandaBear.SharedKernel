using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

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
        result.IsSuccess.ShouldBeTrue();
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
}
