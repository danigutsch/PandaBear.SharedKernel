namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests creation and validation of postal addresses.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ContactValueObjects")]
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
    public void Addresses_with_equal_parts_are_equal()
    {
        // Act
        var first = PostalAddress.Create("Rua A", null, null, "01000", "SP", null, "BR").ShouldBeSuccess();
        var second = PostalAddress.Create(" Rua A ", null, null, "01000", "SP", null, "br").ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
    }
}
