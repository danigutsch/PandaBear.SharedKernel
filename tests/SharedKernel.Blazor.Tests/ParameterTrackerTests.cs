namespace PandaBear.Blazor.Tests;

/// <summary>Tests the helper that tells a component whether a route or component parameter really changed.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ParameterTrackerTests
{
    [Fact]
    public void The_first_value_is_reported_as_initial_even_when_it_is_the_default()
    {
        // Arrange
        var tracker = new ParameterTracker<Guid?>();

        // Act
        var change = tracker.Update(null);

        // Assert
        change.ShouldBe(ParameterChange.Initial);
    }

    [Fact]
    public void The_same_value_again_is_unchanged()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tracker = new ParameterTracker<Guid?>();
        tracker.Update(id);

        // Act
        var change = tracker.Update(id);

        // Assert
        change.ShouldBe(ParameterChange.Unchanged);
    }

    [Fact]
    public void A_different_value_is_a_change_and_becomes_the_current_value()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var tracker = new ParameterTracker<Guid?>();
        tracker.Update(first);

        // Act
        var change = tracker.Update(second);
        var again = tracker.Update(second);

        // Assert
        change.ShouldBe(ParameterChange.Changed);
        again.ShouldBe(ParameterChange.Unchanged);
    }

    [Fact]
    public void Moving_to_and_from_null_counts_as_a_change()
    {
        // Arrange
        var tracker = new ParameterTracker<string?>();
        tracker.Update("edit");

        // Act
        var toNull = tracker.Update(null);
        var back = tracker.Update("edit");

        // Assert
        toNull.ShouldBe(ParameterChange.Changed);
        back.ShouldBe(ParameterChange.Changed);
    }
}
