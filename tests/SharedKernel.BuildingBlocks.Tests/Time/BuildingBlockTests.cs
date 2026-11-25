using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>
/// Tests DateRange creation rules.
/// </summary>
public sealed class BuildingBlockTests
{
    [Fact]
    public void DateRange_create_returns_valid_range_when_end_is_after_start()
    {
        // Arrange
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        result.ShouldBeSuccess();
        result.Value.StartDate.ShouldBe(startDate);
        result.Value.EndDate.ShouldBe(endDate);
        result.Value.DurationDays.ShouldBe(7);
    }

    [Fact]
    public void DateRange_create_rejects_end_dates_equal_to_start()
    {
        // Arrange
        var date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        var error = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        error.ShouldHaveValidationError("schedule", "End date must be after start date.");
    }

    [Fact]
    public void DateRange_create_rejects_end_dates_before_start()
    {
        // Arrange
        var startDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        var error = result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        error.ShouldHaveValidationError("schedule", "End date must be after start date.");
    }
}
