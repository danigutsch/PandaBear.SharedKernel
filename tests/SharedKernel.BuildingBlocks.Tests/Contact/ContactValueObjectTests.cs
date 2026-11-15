using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests creation and validation of email, phone and person-name value objects.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
public sealed class ContactValueObjectTests
{
    [Theory]
    [InlineData("ana@example.com", "ana@example.com")]
    [InlineData("  Ana.Silva+tag@Example.COM  ", "ana.silva+tag@example.com")]
    public void Email_is_trimmed_and_lower_cased(string input, string expected)
    {
        // Act
        var result = EmailAddress.Create(input);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("a@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("a@example")]
    [InlineData("a@.example.com")]
    [InlineData("a@example.")]
    [InlineData("a@exa..mple.com")]
    [InlineData("a b@example.com")]
    public void Email_rejects_invalid_input(string? input)
    {
        // Act
        var result = EmailAddress.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Email_rejects_overlong_address_and_local_part()
    {
        // Arrange
        var overlong = new string('a', 250) + "@example.com";
        var longLocal = new string('a', 65) + "@example.com";

        // Act & Assert
        EmailAddress.Create(overlong).Status.ShouldBe(ResultStatus.Invalid);
        EmailAddress.Create(longLocal).Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Email_equality_ignores_case_and_redacts_text()
    {
        // Arrange
        var first = EmailAddress.Create("Ana@Example.com").ShouldBeSuccess();
        var second = EmailAddress.Create("ana@example.COM").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.ToString().ShouldBe("EmailAddress [redacted]");
    }

    [Theory]
    [InlineData("+55 (11) 91234-5678", "+5511912345678")]
    [InlineData("11 91234-5678", "11912345678")]
    [InlineData("(555) 123.4567", "5551234567")]
    [InlineData("+1234567", "+1234567")]
    public void Phone_is_reduced_to_digits_and_optional_plus(string input, string expected)
    {
        // Act
        var result = PhoneNumber.Create(input);

        // Assert
        result.ShouldBeSuccess().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    [InlineData("12345a7890")]
    [InlineData("+")]
    [InlineData("12+34567890")]
    public void Phone_rejects_invalid_input(string? input)
    {
        // Act
        var result = PhoneNumber.Create(input);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
    }

    [Fact]
    public void Phone_rejects_overlong_input_and_redacts_text()
    {
        // Act
        var overlong = PhoneNumber.Create(new string('1', 65));
        var valid = PhoneNumber.Create("+5511912345678").ShouldBeSuccess();

        // Assert
        overlong.Status.ShouldBe(ResultStatus.Invalid);
        valid.ToString().ShouldBe("PhoneNumber [redacted]");
    }

    [Theory]
    [InlineData(null, "Phone number is required.")]
    [InlineData("12345a7890", "Phone number format is invalid.")]
    [InlineData("123456", "Phone number must have 7 to 15 digits.")]
    public void Phone_messages_name_the_phone_number_by_default(string? input, string expected)
    {
        // Act
        var result = PhoneNumber.Create(input);

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.Keys.ShouldBe(["phone"]);
        errors["phone"].ShouldBe([expected]);
    }

    [Theory]
    [InlineData(null, "WhatsApp is required.")]
    [InlineData("12345a7890", "WhatsApp format is invalid.")]
    [InlineData("123456", "WhatsApp must have 7 to 15 digits.")]
    [InlineData("1111111111111111111111111111111111111111111111111111111111111111111", "WhatsApp is too long.")]
    public void Phone_messages_use_the_given_field_and_label(string? input, string expected)
    {
        // Act
        var result = PhoneNumber.Create(input, field: "whatsapp", label: "WhatsApp");

        // Assert
        var error = result.ErrorDetails.ShouldNotBeNull();
        error.Detail.ShouldBe(expected);
        error.ValidationErrors.ShouldNotBeNull().Keys.ShouldBe(["whatsapp"]);
        error.ValidationErrors["whatsapp"].ShouldBe([expected]);
    }

    [Fact]
    public void Name_is_sanitized_and_exposes_full_name()
    {
        // Act
        var name = PersonName.Create("  Ana   Maria ", "da Silva").ShouldBeSuccess();

        // Assert
        name.FirstName.ShouldBe("Ana Maria");
        name.LastName.ShouldBe("da Silva");
        name.FullName.ShouldBe("Ana Maria da Silva");
        name.ToString().ShouldBe("PersonName [redacted]");
    }

    [Fact]
    public void Name_reports_every_invalid_part()
    {
        // Act
        var result = PersonName.Create(" ", new string('x', PersonName.MaxPartLength + 1));

        // Assert
        var keys = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys;
        keys.ShouldContain("firstName");
        keys.ShouldContain("lastName");
    }

    [Fact]
    public void Names_with_equal_parts_are_equal()
    {
        // Act
        var first = PersonName.Create("Ana", "Silva").ShouldBeSuccess();
        var second = PersonName.Create(" Ana ", "Silva").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
    }
}
