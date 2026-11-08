namespace PandaBear.Blazor.Tests;

/// <summary>Tests the "are you sure" state of a list where at most one row waits for confirmation.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ConfirmationStateTests
{
    [Fact]
    public void Nothing_is_pending_at_first()
    {
        // Arrange
        var state = new ConfirmationState<Guid>();

        // Assert
        state.HasPending.ShouldBeFalse();
        state.IsPending(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void Asking_marks_only_that_key_as_pending()
    {
        // Arrange
        var key = Guid.NewGuid();
        var state = new ConfirmationState<Guid>();

        // Act
        state.Ask(key);

        // Assert
        state.HasPending.ShouldBeTrue();
        state.IsPending(key).ShouldBeTrue();
        state.IsPending(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void Asking_for_another_key_replaces_the_pending_one()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var state = new ConfirmationState<Guid>();
        state.Ask(first);

        // Act
        state.Ask(second);

        // Assert
        state.IsPending(first).ShouldBeFalse();
        state.IsPending(second).ShouldBeTrue();
    }

    [Fact]
    public void Cancelling_clears_the_pending_key()
    {
        // Arrange
        var key = Guid.NewGuid();
        var state = new ConfirmationState<Guid>();
        state.Ask(key);

        // Act
        state.Cancel();

        // Assert
        state.HasPending.ShouldBeFalse();
        state.IsPending(key).ShouldBeFalse();
    }
}
