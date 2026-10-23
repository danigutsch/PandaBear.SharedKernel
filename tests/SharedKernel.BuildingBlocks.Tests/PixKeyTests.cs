using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class PixKeyTests
{
    [Theory]
    [InlineData(PixKeyType.Cpf, "11122233396")]
    [InlineData(PixKeyType.Cnpj, "12345678000195")]
    [InlineData(PixKeyType.Cnpj, "12ABC34501DE35")]
    [InlineData(PixKeyType.Phone, "+5510998765432")]
    [InlineData(PixKeyType.Phone, "+12")]
    [InlineData(PixKeyType.Phone, "+123456789012345")]
    [InlineData(PixKeyType.Email, "pix@bcb.gov.br")]
    [InlineData(PixKeyType.Email, "a+b/c=d@bank")]
    [InlineData(PixKeyType.Evp, "aaaaaaaa-aaaa-1aaa-8aaa-aaaaaaaaaaaa")]
    public void Creates_explicitly_typed_dict_formats(PixKeyType type, string value)
    {
        // Act
        var result = PixKey.Create(type, value);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Type.ShouldBe(type);
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(PixKeyType.Cpf, null)]
    [InlineData(PixKeyType.Cpf, "")]
    [InlineData(PixKeyType.Cpf, "12345678901")]
    [InlineData(PixKeyType.Cpf, "12345678000195")]
    [InlineData(PixKeyType.Cnpj, "11122233396")]
    [InlineData(PixKeyType.Cnpj, "12abc34501de35")]
    [InlineData(PixKeyType.Cnpj, "12ABC34501DE34")]
    [InlineData(PixKeyType.Phone, "+1")]
    [InlineData(PixKeyType.Phone, "+0123456789")]
    [InlineData(PixKeyType.Phone, "5510998765432")]
    [InlineData(PixKeyType.Phone, "+1234567890123456")]
    [InlineData(PixKeyType.Phone, "+5510998765432\n")]
    [InlineData(PixKeyType.Phone, "+５510998765432")]
    [InlineData(PixKeyType.Email, "Pix@bcb.gov.br")]
    [InlineData(PixKeyType.Email, "pix@bcb.gov.br\n")]
    [InlineData(PixKeyType.Email, "pix@-bcb.gov.br")]
    [InlineData(PixKeyType.Email, "pix@bcb-.gov.br")]
    [InlineData(PixKeyType.Email, "pix@bcb..gov.br")]
    [InlineData(PixKeyType.Email, "pix@éxample.com")]
    [InlineData(PixKeyType.Email, "pix@bcb.gov.br ")]
    [InlineData(PixKeyType.Evp, "AAAAAAAA-AAAA-1AAA-8AAA-AAAAAAAAAAAA")]
    [InlineData(PixKeyType.Evp, "{aaaaaaaa-aaaa-1aaa-8aaa-aaaaaaaaaaaa}")]
    [InlineData(PixKeyType.Evp, "aaaaaaaaaaaa1aaa8aaaaaaaaaaaaaaa")]
    [InlineData(PixKeyType.Evp, "aaaaaaaa-aaaa-1aaa-8aaa-aaaaaaaaaaaa\n")]
    [InlineData((PixKeyType)0, "pix@bcb.gov.br")]
    [InlineData((PixKeyType)99, "pix@bcb.gov.br")]
    public void Rejects_mismatched_types_and_noncanonical_values(PixKeyType type, string? value)
    {
        // Act
        var result = PixKey.Create(type, value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Theory]
    [InlineData(77, true)]
    [InlineData(78, false)]
    [InlineData(10_000, false)]
    public void Applies_dict_email_length_limit(int length, bool expected)
    {
        // Arrange
        var value = new string('a', length - 2) + "@b";

        // Act
        var result = PixKey.Create(PixKeyType.Email, value);

        // Assert
        result.IsSuccess.ShouldBe(expected);
    }

    [Fact]
    public void Rejects_a_domain_label_longer_than_sixty_three_characters()
    {
        // Arrange
        var value = "a@" + new string('b', 64);

        // Act
        var result = PixKey.Create(PixKeyType.Email, value);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }
}
