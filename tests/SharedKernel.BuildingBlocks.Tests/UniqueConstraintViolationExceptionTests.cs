namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the persistence-neutral unique-rule exception.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "UniqueViolations")]
public sealed class UniqueConstraintViolationExceptionTests
{
    [Fact]
    public void It_names_the_rule_and_keeps_the_cause()
    {
        // Arrange
        var cause = new InvalidOperationException("provider");

        // Act
        var exception = new UniqueConstraintViolationException("customer.email", cause);

        // Assert
        exception.Rule.ShouldBe("customer.email");
        exception.InnerException.ShouldBeSameAs(cause);
        exception.Message.ShouldContain("customer.email", StringComparison.Ordinal);
    }

    [Fact]
    public void The_rule_only_constructor_has_no_cause()
    {
        // Act
        var exception = new UniqueConstraintViolationException("tour.identifier");

        // Assert
        exception.Rule.ShouldBe("tour.identifier");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void The_parameterless_constructor_uses_an_unknown_rule()
    {
        // Act
        var exception = new UniqueConstraintViolationException();

        // Assert
        exception.Rule.ShouldBe("unknown");
    }
}
