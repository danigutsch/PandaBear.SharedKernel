using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class CnpjTests
{
    [Theory]
    [InlineData("12345678000195")]
    [InlineData("00000000000191")]
    [InlineData("12ABC34501DE35")]
    [InlineData("00000000E08G12")]
    public void Creates_numeric_and_alphanumeric_cnpj_preserving_leading_zeros(string value)
    {
        // Act
        var result = Cnpj.Create(value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678000194")]
    [InlineData("12345678000185")]
    [InlineData("12ABC34501DE34")]
    [InlineData("12ABC34501DE25")]
    [InlineData("12abc34501de35")]
    [InlineData("12ABC34501DE3A")]
    [InlineData("00000000000000")]
    [InlineData("11111111111111")]
    [InlineData("12.ABC.345/01DE-35")]
    [InlineData("12ABC34501DE35\n")]
    [InlineData("12ABC34501DE3")]
    [InlineData("12ABC34501DE355")]
    [InlineData("１２ABC34501DE35")]
    public void Rejects_invalid_or_noncanonical_cnpj(string? value)
    {
        // Act
        var result = Cnpj.Create(value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }
}
