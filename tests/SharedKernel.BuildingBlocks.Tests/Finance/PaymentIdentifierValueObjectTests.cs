namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Tests equality, hashing and redaction shared by the payment identifier value objects Iban, Bic, Cpf, Cnpj and PixKey.
/// </summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "PaymentIdentifiers")]
public sealed class PaymentIdentifierValueObjectTests
{
    [Fact]
    public void Compares_identifiers_by_type_and_value_and_preserves_hash_consistency()
    {
        // Arrange
        ValueObject[] first =
        [
            Iban.Create("DE89370400440532013000").Value,
            Bic.Create("DEUTDEFF").Value,
            Cpf.Create("11122233396").Value,
            Cnpj.Create("12ABC34501DE35").Value,
            PixKey.Create(PixKeyType.Cpf, "11122233396").Value
        ];
        ValueObject[] second =
        [
            Iban.Create("DE89370400440532013000").Value,
            Bic.Create("DEUTDEFF").Value,
            Cpf.Create("11122233396").Value,
            Cnpj.Create("12ABC34501DE35").Value,
            PixKey.Create(PixKeyType.Cpf, "11122233396").Value
        ];

        // Act
        var equal = first.SequenceEqual(second);
        var matchingHashes = first.Select(static value => value.GetHashCode())
            .SequenceEqual(second.Select(static value => value.GetHashCode()));
        var differentTypes = first[2].Equals(first[4]);

        // Assert
        equal.ShouldBeTrue();
        matchingHashes.ShouldBeTrue();
        differentTypes.ShouldBeFalse();
    }

    [Fact]
    public void Distinguishes_different_identifier_values()
    {
        // Arrange
        ValueObject[] first =
        [
            Iban.Create("DE89370400440532013000").Value,
            Bic.Create("DEUTDEFF").Value,
            Cpf.Create("11122233396").Value,
            Cnpj.Create("12345678000195").Value,
            PixKey.Create(PixKeyType.Email, "a@b").Value
        ];
        ValueObject[] second =
        [
            Iban.Create("AT611904300234573201").Value,
            Bic.Create("DEUTDEFF500").Value,
            Cpf.Create("52998224725").Value,
            Cnpj.Create("12ABC34501DE35").Value,
            PixKey.Create(PixKeyType.Email, "b@b").Value
        ];

        // Act
        var allDifferent = first.Zip(second).All(static pair => !pair.First.Equals(pair.Second));

        // Assert
        allDifferent.ShouldBeTrue();
    }

    [Fact]
    public void Redacts_all_identifier_diagnostic_strings()
    {
        // Arrange
        ValueObject[] values =
        [
            Iban.Create("DE89370400440532013000").Value,
            Bic.Create("DEUTDEFF").Value,
            Cpf.Create("11122233396").Value,
            Cnpj.Create("12ABC34501DE35").Value,
            PixKey.Create(PixKeyType.Email, "pix@bcb.gov.br").Value
        ];

        // Act
        var diagnostics = values.Select(static value => value.ToString()).ToArray();

        // Assert
        diagnostics.ShouldBe(["Iban [redacted]", "Bic [redacted]", "Cpf [redacted]", "Cnpj [redacted]", "PixKey [redacted]"]);
    }

    [Theory]
    [InlineData("sensitive-recipient-value")]
    [InlineData(null)]
    public void Keeps_errors_independent_of_rejected_input(string? value)
    {
        // Act
        var errors = new[]
        {
            Iban.Create(value).ErrorDetails,
            Bic.Create(value).ErrorDetails,
            Cpf.Create(value).ErrorDetails,
            Cnpj.Create(value).ErrorDetails,
            PixKey.Create(PixKeyType.Email, value).ErrorDetails
        };

        // Assert
        var details = errors.Select(static error => error.ShouldNotBeNull().Detail).ToArray();
        details.ShouldBe(["Invalid canonical IBAN.", "Invalid canonical BIC.", "Invalid canonical CPF.",
            "Invalid canonical CNPJ.", "Invalid canonical Pix key."]);
        var messages = errors.SelectMany(static error => error.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Values)
            .SelectMany(static messages => messages).ToArray();
        messages.ShouldBe(details);
    }
}
