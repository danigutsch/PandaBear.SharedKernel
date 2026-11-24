namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests creation and validation of postal addresses.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ContactValueObjects")]
public sealed class PostalAddressTests
{
    [Fact]
    public void Address_is_sanitized_and_blank_optionals_become_null()
    {
        // Act
        var address = PostalAddress.Create(" Rua  A, 10 ", "  ", null, " 01000-000 ", " São Paulo ", "", "br").ShouldBeSuccess();

        // Assert
        address.Street.ShouldBe("Rua A, 10");
        address.Complement.ShouldBeNull();
        address.Neighborhood.ShouldBeNull();
        address.PostalCode.ShouldBe("01000-000");
        address.City.ShouldBe("São Paulo");
        address.State.ShouldBeNull();
        address.Country.Value.ShouldBe("BR");
        address.ToString().ShouldBe("PostalAddress [redacted]");
    }

    [Fact]
    public void Address_keeps_optional_parts_when_given()
    {
        // Act
        var address = PostalAddress.Create("Rua A", "Apt 4", "Centro", "01000", "Sao Paulo", "SP", "BR").ShouldBeSuccess();

        // Assert
        address.Complement.ShouldBe("Apt 4");
        address.Neighborhood.ShouldBe("Centro");
        address.State.ShouldBe("SP");
    }

    [Fact]
    public void Address_reports_every_invalid_part()
    {
        // Act
        var result = PostalAddress.Create(null, new string('c', PostalAddress.MaxPartLength + 1), new string('n', PostalAddress.MaxPartLength + 1), new string('9', PostalAddress.MaxPostalCodeLength + 1), "", new string('s', PostalAddress.MaxPartLength + 1), "ZZ");

        // Assert
        var keys = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().Keys;
        foreach (var field in new[] { "street", "complement", "neighborhood", "postalCode", "city", "state", "country" })
        {
            keys.ShouldContain(field);
        }
    }

    [Fact]
    public void Address_messages_name_the_parts_in_english_by_default()
    {
        // Act
        var result = PostalAddress.Create(null, new string('c', PostalAddress.MaxPartLength + 1), null, " ", "", null, "ZZ");

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors["street"].ShouldBe(["Street is required."]);
        errors["complement"].ShouldBe([$"Complement cannot exceed {PostalAddress.MaxPartLength} characters."]);
        errors["postalCode"].ShouldBe(["Postal code is required."]);
        errors["city"].ShouldBe(["City is required."]);
        errors["country"].ShouldBe(["Country must be a valid ISO 3166-1 alpha-2 code."]);
    }

    [Fact]
    public void Address_messages_use_the_given_labels_and_keep_the_field_keys()
    {
        // Arrange
        var labels = new PostalAddressLabels
        {
            Street = "Street and number",
            Complement = "Apartment",
            Neighborhood = "District",
            PostalCode = "CEP",
            City = "Town",
            State = "Province",
            Country = "Country of the address",
        };
        var tooLong = new string('x', PostalAddress.MaxPartLength + 1);

        // Act
        var result = PostalAddress.Create(" ", tooLong, tooLong, null, null, tooLong, null, labels);

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["city", "complement", "country", "neighborhood", "postalCode", "state", "street"]);
        errors["street"].ShouldBe(["Street and number is required."]);
        errors["complement"].ShouldBe([$"Apartment cannot exceed {PostalAddress.MaxPartLength} characters."]);
        errors["neighborhood"].ShouldBe([$"District cannot exceed {PostalAddress.MaxPartLength} characters."]);
        errors["postalCode"].ShouldBe(["CEP is required."]);
        errors["city"].ShouldBe(["Town is required."]);
        errors["state"].ShouldBe([$"Province cannot exceed {PostalAddress.MaxPartLength} characters."]);
        errors["country"].ShouldBe(["Country of the address must be a valid ISO 3166-1 alpha-2 code."]);
    }

    [Fact]
    public void Address_labels_default_to_the_english_part_names()
    {
        // Act
        var labels = PostalAddressLabels.Default;

        // Assert
        string[] names = [labels.Street, labels.Complement, labels.Neighborhood, labels.PostalCode, labels.City, labels.State, labels.Country];
        names.ShouldBe(["Street", "Complement", "Neighborhood", "Postal code", "City", "State", "Country"]);
    }

    [Fact]
    public void Addresses_with_equal_parts_are_equal()
    {
        // Act
        var first = PostalAddress.Create("Rua A", null, null, "01000", "SP", null, "BR").ShouldBeSuccess();
        var second = PostalAddress.Create(" Rua A ", null, null, "01000", "SP", null, "br").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
    }

    [Fact]
    public void Malformed_text_is_an_invalid_part_rather_than_an_exception()
    {
        // Act
        var result = PostalAddress.Create("Rua \uD800", "\uDC00", null, "01000", "Sao Paulo", null, "BR");

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors["street"].ShouldBe(["Street is not valid text."]);
        errors["complement"].ShouldBe(["Complement is not valid text."]);
    }
}
