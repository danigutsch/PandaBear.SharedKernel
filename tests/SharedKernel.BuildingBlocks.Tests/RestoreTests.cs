namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the persistence-only Restore factories, which rebuild stored text without re-running input validation.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class RestoreTests
{
    [Theory]
    [InlineData("ana@example.com")]
    [InlineData("  Ana.Silva+tag@Example.COM  ")]
    public void Email_restore_equals_create_for_valid_values(string input)
    {
        // Act
        var restored = EmailAddress.Restore(input);

        // Assert
        restored.ShouldBe(EmailAddress.Create(input).ShouldBeSuccess());
        restored.GetHashCode().ShouldBe(EmailAddress.Create(input).ShouldBeSuccess().GetHashCode());
    }

    [Fact]
    public void Email_restore_keeps_a_stored_value_that_create_rejects()
    {
        // Act
        var restored = EmailAddress.Restore("Legacy@localhost");

        // Assert
        EmailAddress.Create("Legacy@localhost").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Value.ShouldBe("legacy@localhost");
        restored.ToString().ShouldBe("EmailAddress [redacted]");
    }

    [Theory]
    [InlineData("+55 (11) 91234-5678")]
    [InlineData("11912345678")]
    public void Phone_restore_equals_create_for_valid_values(string input)
    {
        // Act
        var restored = PhoneNumber.Restore(input);

        // Assert
        restored.ShouldBe(PhoneNumber.Create(input).ShouldBeSuccess());
    }

    [Fact]
    public void Phone_restore_keeps_a_stored_value_that_create_rejects()
    {
        // Act
        var restored = PhoneNumber.Restore(" +55 11 x123 ");

        // Assert
        PhoneNumber.Create("+55 11 x123").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Value.ShouldBe("+55 11 x123");
        restored.ToString().ShouldBe("PhoneNumber [redacted]");
    }

    [Theory]
    [InlineData("BR")]
    [InlineData(" pt ")]
    public void Country_restore_equals_create_for_valid_values(string input)
    {
        // Act
        var restored = CountryCode.Restore(input);

        // Assert
        restored.ShouldBe(CountryCode.Create(input).ShouldBeSuccess());
        restored.EnglishName.ShouldBe(CountryCode.Create(input).ShouldBeSuccess().EnglishName);
    }

    [Fact]
    public void Country_restore_returns_a_value_for_a_code_outside_the_assigned_set()
    {
        // Act
        var restored = CountryCode.Restore("xk");

        // Assert
        CountryCode.Create("XK").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Value.ShouldBe("XK");
        restored.EnglishName.ShouldBe("XK");
        restored.ToString().ShouldBe("XK");
    }

    [Theory]
    [InlineData("BRL")]
    [InlineData(" eur ")]
    public void Currency_restore_equals_create_for_valid_values(string input)
    {
        // Act
        var restored = Currency.Restore(input);

        // Assert
        restored.ShouldBe(Currency.Create(input).ShouldBeSuccess());
        restored.MinorUnits.ShouldBe(Currency.Create(input).ShouldBeSuccess().MinorUnits);
    }

    [Fact]
    public void Currency_restore_returns_a_value_for_a_code_the_allow_list_now_rejects()
    {
        // Act
        var restored = Currency.Restore("hrk");

        // Assert
        Currency.Create("HRK").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Code.ShouldBe("HRK");
        restored.MinorUnits.ShouldBe(2);
        restored.ToString().ShouldBe("HRK");
    }

    [Fact]
    public void Money_restore_keeps_the_stored_amount_and_currency_without_rounding()
    {
        // Arrange
        var stored = Currency.Restore("HRK");

        // Act
        var restored = Money.Restore(10.005m, stored);

        // Assert
        restored.Amount.ShouldBe(10.005m);
        restored.Currency.ShouldBe(stored);
    }

    [Fact]
    public void Money_restore_equals_create_for_an_already_rounded_amount()
    {
        // Act
        var restored = Money.Restore(1999.99m, Currency.Brl);

        // Assert
        restored.ShouldBe(Money.Create(1999.99m, Currency.Brl).ShouldBeSuccess());
    }

    [Fact]
    public void Money_restore_rejects_a_missing_currency()
    {
        // Arrange
        var restore = () => Money.Restore(1m, null!);

        // Act & Assert
        restore.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Person_name_restore_equals_create_for_valid_values()
    {
        // Act
        var restored = PersonName.Restore("  Ana ", "Silva");

        // Assert
        restored.ShouldBe(PersonName.Create("Ana", "Silva").ShouldBeSuccess());
        restored.ToString().ShouldBe("PersonName [redacted]");
    }

    [Fact]
    public void Person_name_restore_keeps_a_part_longer_than_the_current_limit()
    {
        // Arrange
        var longName = new string('a', PersonName.MaxPartLength + 10);

        // Act
        var restored = PersonName.Restore(longName, "Silva");

        // Assert
        PersonName.Create(longName, "Silva").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.FirstName.ShouldBe(longName);
    }

    [Theory]
    [InlineData("52998224725", "52998224725")]
    [InlineData(" 01234567890 ", "01234567890")]
    public void Cpf_restore_equals_create_for_valid_values(string input, string canonical)
    {
        // Arrange
        var created = Cpf.Create(canonical).ShouldBeSuccess();

        // Act
        var restored = Cpf.Restore(input);

        // Assert
        restored.ShouldBe(created);
        restored.GetHashCode().ShouldBe(created.GetHashCode());
        restored.ToString().ShouldBe("Cpf [redacted]");
    }

    [Fact]
    public void Cpf_restore_keeps_a_stored_value_that_create_rejects()
    {
        // Act
        var restored = Cpf.Restore("11111111111");

        // Assert
        Cpf.Create("11111111111").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Value.ShouldBe("11111111111");
        restored.ToString().ShouldBe("Cpf [redacted]");
    }

    [Fact]
    public void Postal_address_restore_equals_create_for_valid_values()
    {
        // Arrange
        var created = PostalAddress.Create("Rua A, 10", "Apt 4", null, "01000-000", "São Paulo", "SP", "BR").ShouldBeSuccess();

        // Act
        var restored = PostalAddress.Restore(" Rua  A, 10 ", "Apt 4", null, "01000-000", "São Paulo", "SP", CountryCode.Restore("br"));

        // Assert
        restored.ShouldBe(created);
        restored.GetHashCode().ShouldBe(created.GetHashCode());
        restored.ToString().ShouldBe("PostalAddress [redacted]");
    }

    [Fact]
    public void Postal_address_restore_turns_blank_optional_parts_into_null()
    {
        // Act
        var restored = PostalAddress.Restore("Rua A", "  ", "", "01000", "Sao Paulo", null, CountryCode.Restore("BR"));

        // Assert
        restored.Complement.ShouldBeNull();
        restored.Neighborhood.ShouldBeNull();
        restored.State.ShouldBeNull();
    }

    [Fact]
    public void Postal_address_restore_keeps_parts_longer_than_the_current_limit_and_an_unassigned_country()
    {
        // Arrange
        var longStreet = new string('a', PostalAddress.MaxPartLength + 10);
        var longPostalCode = new string('9', PostalAddress.MaxPostalCodeLength + 10);

        // Act
        var restored = PostalAddress.Restore(longStreet, null, null, longPostalCode, "Pristina", null, CountryCode.Restore("xk"));

        // Assert
        PostalAddress.Create(longStreet, null, null, longPostalCode, "Pristina", null, "XK").Status.ShouldBe(Results.ResultStatus.Invalid);
        restored.Street.ShouldBe(longStreet);
        restored.PostalCode.ShouldBe(longPostalCode);
        restored.Country.Value.ShouldBe("XK");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Restore_rejects_blank_text(string input)
    {
        // Arrange
        var email = () => EmailAddress.Restore(input);
        var phone = () => PhoneNumber.Restore(input);
        var country = () => CountryCode.Restore(input);
        var currency = () => Currency.Restore(input);
        var firstName = () => PersonName.Restore(input, "Silva");
        var lastName = () => PersonName.Restore("Ana", input);
        var cpf = () => Cpf.Restore(input);
        var street = () => PostalAddress.Restore(input, null, null, "01000", "Sao Paulo", null, CountryCode.Restore("BR"));
        var postalCode = () => PostalAddress.Restore("Rua A", null, null, input, "Sao Paulo", null, CountryCode.Restore("BR"));
        var city = () => PostalAddress.Restore("Rua A", null, null, "01000", input, null, CountryCode.Restore("BR"));

        // Act & Assert
        cpf.ShouldThrow<ArgumentException>();
        street.ShouldThrow<ArgumentException>();
        postalCode.ShouldThrow<ArgumentException>();
        city.ShouldThrow<ArgumentException>();
        email.ShouldThrow<ArgumentException>();
        phone.ShouldThrow<ArgumentException>();
        country.ShouldThrow<ArgumentException>();
        currency.ShouldThrow<ArgumentException>();
        firstName.ShouldThrow<ArgumentException>();
        lastName.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Restore_rejects_null_text_as_a_mapper_bug()
    {
        // Arrange
        var email = () => EmailAddress.Restore(null!);
        var phone = () => PhoneNumber.Restore(null!);
        var country = () => CountryCode.Restore(null!);
        var currency = () => Currency.Restore(null!);
        var name = () => PersonName.Restore(null!, null!);
        var cpf = () => Cpf.Restore(null!);
        var street = () => PostalAddress.Restore(null!, null, null, "01000", "Sao Paulo", null, CountryCode.Restore("BR"));
        var postalCode = () => PostalAddress.Restore("Rua A", null, null, null!, "Sao Paulo", null, CountryCode.Restore("BR"));
        var city = () => PostalAddress.Restore("Rua A", null, null, "01000", null!, null, CountryCode.Restore("BR"));
        var addressCountry = () => PostalAddress.Restore("Rua A", null, null, "01000", "Sao Paulo", null, null!);

        // Act & Assert
        cpf.ShouldThrow<ArgumentNullException>();
        street.ShouldThrow<ArgumentNullException>();
        postalCode.ShouldThrow<ArgumentNullException>();
        city.ShouldThrow<ArgumentNullException>();
        addressCountry.ShouldThrow<ArgumentNullException>();
        email.ShouldThrow<ArgumentNullException>();
        phone.ShouldThrow<ArgumentNullException>();
        country.ShouldThrow<ArgumentNullException>();
        currency.ShouldThrow<ArgumentNullException>();
        name.ShouldThrow<ArgumentNullException>();
    }
}
