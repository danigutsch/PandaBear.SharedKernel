namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the persistence-neutral concurrency conflict exception.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ConcurrencyConflicts")]
public sealed class ConcurrencyConflictExceptionTests
{
    [Fact]
    public void The_default_constructor_describes_the_conflict_without_a_cause()
    {
        // Act
        var exception = new ConcurrencyConflictException();

        // Assert
        exception.Message.ShouldContain("changed or was deleted", StringComparison.Ordinal);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void The_message_constructor_keeps_the_message()
    {
        // Act
        var exception = new ConcurrencyConflictException("custom");

        // Assert
        exception.Message.ShouldBe("custom");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void The_cause_constructor_keeps_the_provider_exception()
    {
        // Arrange
        var cause = new InvalidOperationException("provider");

        // Act
        var exception = new ConcurrencyConflictException("custom", cause);

        // Assert
        exception.Message.ShouldBe("custom");
        exception.InnerException.ShouldBeSameAs(cause);
    }

    [Fact]
    public void The_cause_only_constructor_uses_the_default_message()
    {
        // Arrange
        var cause = new InvalidOperationException("provider");

        // Act
        var exception = new ConcurrencyConflictException(cause);

        // Assert
        exception.Message.ShouldBe(new ConcurrencyConflictException().Message);
        exception.InnerException.ShouldBeSameAs(cause);
    }
}
