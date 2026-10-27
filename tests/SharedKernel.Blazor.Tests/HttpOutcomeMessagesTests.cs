namespace PandaBear.Blazor.Tests;

/// <summary>Tests the caller-supplied copy mapping for HTTP outcomes.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class HttpOutcomeMessagesTests
{
    [Fact]
    public void Configured_kind_returns_its_copy_and_other_kinds_return_the_fallback()
    {
        // Arrange
        var messages = new HttpOutcomeMessages("Try again later.")
            .With(HttpOutcomeKind.NotFound, "Not found.");

        // Act
        var notFound = messages.For(HttpOutcomeKind.NotFound);
        var conflict = messages.For(HttpOutcomeKind.Conflict);

        // Assert
        notFound.ShouldBe("Not found.");
        conflict.ShouldBe("Try again later.");
    }

    [Fact]
    public void Outcome_overload_never_uses_server_text()
    {
        // Arrange
        var messages = new HttpOutcomeMessages("Try again later.");
        var outcome = new HttpOutcome
        {
            Kind = HttpOutcomeKind.UnexpectedStatus,
            Message = HostileText.StackTrace,
            ValidationErrors = new Dictionary<string, string[]> { ["Name"] = [HostileText.Markup] },
        };

        // Act
        var message = messages.For(outcome);

        // Assert
        message.ShouldBe("Try again later.");
    }

    [Fact]
    public void With_does_not_change_the_original_catalog()
    {
        // Arrange
        var original = new HttpOutcomeMessages("Fallback.");

        // Act
        var changed = original.With(HttpOutcomeKind.Forbidden, "Forbidden.");

        // Assert
        original.For(HttpOutcomeKind.Forbidden).ShouldBe("Fallback.");
        changed.For(HttpOutcomeKind.Forbidden).ShouldBe("Forbidden.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_copy_is_rejected(string copy)
    {
        // Arrange
        var messages = new HttpOutcomeMessages("Fallback.");

        // Act
        var constructor = () => new HttpOutcomeMessages(copy);
        var with = () => messages.With(HttpOutcomeKind.NotFound, copy);

        // Assert
        constructor.ShouldThrow<ArgumentException>();
        with.ShouldThrow<ArgumentException>();
    }
}
