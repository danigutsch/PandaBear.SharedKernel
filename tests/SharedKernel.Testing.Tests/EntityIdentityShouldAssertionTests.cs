namespace PandaBear.Testing.Tests;

public sealed class EntityIdentityShouldAssertionTests
{
    [Theory]
    [InlineData("018f23a4-1234-7000-8000-000000000001")]
    [InlineData("018f23a4-1234-7000-9000-000000000001")]
    [InlineData("018f23a4-1234-7000-a000-000000000001")]
    [InlineData("018f23a4-1234-7000-b000-000000000001")]
    public void Uuid_v7_assertion_accepts_standard_variant_identifiers(string text)
    {
        // Arrange
        var id = Guid.Parse(text);

        // Act, Assert
        id.ShouldBeUuidV7();
    }

    [Fact]
    public void Uuid_v7_assertion_accepts_generated_identifiers()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act, Assert
        id.ShouldBeUuidV7();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("018f23a4-1234-4000-8000-000000000001")]
    [InlineData("018f23a4-1234-6000-8000-000000000001")]
    public void Uuid_v7_assertion_rejects_other_versions(string text)
    {
        // Arrange
        var id = Guid.Parse(text);
        Action assertion = () => id.ShouldBeUuidV7();

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.EqualException>();
    }

    [Theory]
    [InlineData("018f23a4-1234-7000-0000-000000000001")]
    [InlineData("018f23a4-1234-7000-c000-000000000001")]
    [InlineData("018f23a4-1234-7000-e000-000000000001")]
    public void Uuid_v7_assertion_rejects_other_variants(string text)
    {
        // Arrange
        var id = Guid.Parse(text);
        Action assertion = () => id.ShouldBeUuidV7();

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void Identity_assertion_supports_private_init_setters_and_clears_the_second_identity()
    {
        // Arrange
        var first = new IdentityAssertionEntity();
        var second = new IdentityAssertionEntity();
        var different = new IdentityAssertionEntity();

        // Act
        first.ShouldHaveGeneratedIdentitySemantics(second, different);

        // Assert
        first.Id.ShouldBeUuidV7();
        second.Id.ShouldBe(Guid.Empty);
        different.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public void Identity_assertion_rejects_reference_equality()
    {
        // Arrange
        var first = new ReferenceIdentityAssertionEntity();
        var second = new ReferenceIdentityAssertionEntity();
        var different = new ReferenceIdentityAssertionEntity();
        Action assertion = () => first.ShouldHaveGeneratedIdentitySemantics(second, different);

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.TrueException>();
    }

    [Fact]
    public void Identity_assertion_rejects_inconsistent_hash_codes_for_equal_identities()
    {
        // Arrange
        var first = new InconsistentHashIdentityAssertionEntity(1);
        var second = new InconsistentHashIdentityAssertionEntity(2);
        var different = new InconsistentHashIdentityAssertionEntity(3);
        Action assertion = () => first.ShouldHaveGeneratedIdentitySemantics(second, different);

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.EqualException>();
    }

    [Fact]
    public void Identity_assertion_rejects_types_without_an_identity_property()
    {
        // Arrange
        var first = new object();
        var second = new object();
        var different = new object();
        Action assertion = () => first.ShouldHaveGeneratedIdentitySemantics(second, different);

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.NotNullException>();
    }

    [Fact]
    public void Identity_assertion_rejects_identity_properties_without_setters()
    {
        // Arrange
        var first = new
        {
            Id = Guid.Empty
        };
        var second = new
        {
            Id = Guid.Empty
        };
        var different = new
        {
            Id = Guid.Empty
        };
        Action assertion = () => first.ShouldHaveGeneratedIdentitySemantics(second, different);

        // Act, Assert
        assertion.ShouldThrow<Xunit.Sdk.NotNullException>();
    }

    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("different")]
    public void Identity_assertion_rejects_null_instances(string parameter)
    {
        // Arrange
        var first = new IdentityAssertionEntity();
        var second = new IdentityAssertionEntity();
        var different = new IdentityAssertionEntity();
        var actualFirst = parameter == "first" ? null! : first;
        Action assertion = () => actualFirst.ShouldHaveGeneratedIdentitySemantics(
            parameter == "second" ? null! : second,
            parameter == "different" ? null! : different);

        // Act
        var exception = assertion.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe(parameter);
    }
}
