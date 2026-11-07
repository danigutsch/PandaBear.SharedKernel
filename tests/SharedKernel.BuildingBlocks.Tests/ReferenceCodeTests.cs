namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the case-insensitive reference code, such as a tour identifier.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ValueObjects")]
public sealed class ReferenceCodeTests
{
    [Theory]
    [InlineData("alps-2027", "ALPS-2027")]
    [InlineData("  Alps_2027 ", "ALPS_2027")]
    [InlineData("A1", "A1")]
    public void A_code_is_trimmed_and_upper_cased(string input, string expected)
    {
        // Act
        var code = ReferenceCode.Create(input, maxLength: 64).ShouldBeSuccess();

        // Assert
        code.Value.ShouldBe(expected);
    }

    [Fact]
    public void Codes_that_differ_only_in_case_are_equal()
    {
        // Act
        var first = ReferenceCode.Create("alps-2027", 64).Value;
        var second = ReferenceCode.Create("ALPS-2027", 64).Value;

        // Assert
        first.ShouldBe(second);
    }

    [Theory]
    [InlineData(null, "Identifier is required.")]
    [InlineData("   ", "Identifier is required.")]
    [InlineData("has space", "Identifier can only contain letters, digits, '-' and '_'.")]
    [InlineData("bad/char", "Identifier can only contain letters, digits, '-' and '_'.")]
    [InlineData("caf\u00e9", "Identifier can only contain letters, digits, '-' and '_'.")]
    public void An_invalid_code_is_rejected_with_a_message_that_does_not_echo_it(string? input, string message)
    {
        // Act
        var result = ReferenceCode.Create(input, 64, field: "identifier", label: "Identifier");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["identifier"].ShouldBe([message]);
    }

    [Fact]
    public void An_overlong_code_is_rejected()
    {
        // Act
        var result = ReferenceCode.Create(new string('a', 11), 10, field: "identifier", label: "Identifier");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["identifier"].ShouldBe(["Identifier cannot exceed 10 characters."]);
    }
}
