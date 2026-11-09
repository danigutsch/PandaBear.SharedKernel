using PandaBear.Results;

namespace PandaBear.Mediator.Tests;

/// <summary>Tests the stale-data conflict results a handler returns when the loaded version is outdated.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ConcurrencyConflictResultsTests
{
    [Fact]
    public void Stale_is_a_conflict_with_the_message_of_the_behavior()
    {
        // Arrange
        // Act
        var result = ConcurrencyConflictResults.Stale();

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.RenameTour, Result>.ConflictMessage);
    }

    [Fact]
    public void Stale_with_a_value_is_a_conflict_with_the_message_of_the_behavior()
    {
        // Arrange
        // Act
        var result = ConcurrencyConflictResults.Stale<string>();

        // Assert
        result.Status.ShouldBe(ResultStatus.Conflict);
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe(ConcurrencyConflictBehavior<ConcurrencyConflictTestTypes.LoadTour, Result<string>>.ConflictMessage);
    }
}
