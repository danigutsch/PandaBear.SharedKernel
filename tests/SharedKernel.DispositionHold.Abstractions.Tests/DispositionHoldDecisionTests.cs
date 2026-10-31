namespace PandaBear.DispositionHold.Tests;

/// <summary>
/// Tests DispositionHoldDecision semantics.
/// </summary>
public sealed class DispositionHoldDecisionTests
{
    [Fact]
    public void Clear_permits_disposition_and_names_no_hold()
    {
        // Arrange, Act
        var decision = DispositionHoldDecision.Clear;

        // Assert
        decision.IsClear.ShouldBeTrue();
        decision.HoldIds.ShouldBeEmpty();
    }

    [Fact]
    public void Default_decision_is_clear_only_because_it_names_no_hold()
    {
        // Arrange, Act
        var decision = default(DispositionHoldDecision);

        // Assert
        decision.HoldIds.ShouldBeEmpty();
    }

    [Fact]
    public void HeldBy_forbids_disposition_and_names_the_holds()
    {
        // Arrange
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];

        // Act
        var decision = DispositionHoldDecision.HeldBy(ids);

        // Assert
        decision.IsClear.ShouldBeFalse();
        decision.HoldIds.ShouldBe(ids);
    }

    [Fact]
    public void HeldBy_rejects_null_and_empty_input()
    {
        // Arrange, Act
        Action nullIds = () => DispositionHoldDecision.HeldBy(null!);
        Action noIds = () => DispositionHoldDecision.HeldBy([]);

        // Assert
        _ = nullIds.ShouldThrow<ArgumentNullException>();
        _ = noIds.ShouldThrow<ArgumentException>();
    }
}
