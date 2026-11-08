namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the validated range of calendar days.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ValueObjects")]
public sealed class DateOnlyRangeTests
{
    [Fact]
    public void A_range_counts_both_its_first_and_its_last_day()
    {
        // Act
        var range = DateOnlyRange.Create(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 11)).ShouldBeSuccess();

        // Assert
        range.StartDate.ShouldBe(new DateOnly(2027, 6, 7));
        range.EndDate.ShouldBe(new DateOnly(2027, 6, 11));
        range.Days.ShouldBe(5);
    }

    [Fact]
    public void A_one_day_range_is_allowed()
    {
        // Act
        var range = DateOnlyRange.Create(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 7)).ShouldBeSuccess();

        // Assert
        range.Days.ShouldBe(1);
    }

    [Fact]
    public void An_end_before_the_start_is_rejected_on_the_end_date()
    {
        // Act
        var result = DateOnlyRange.Create(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 6));

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["endDate"].ShouldBe(["End date cannot be before start date."]);
    }

    [Fact]
    public void Missing_dates_are_reported_as_required_and_not_as_an_order_error()
    {
        // Act
        var result = DateOnlyRange.Create(default, default);

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors["startDate"].ShouldBe(["Start date is required."]);
        errors["endDate"].ShouldBe(["End date is required."]);
    }

    [Fact]
    public void Only_the_missing_date_is_reported()
    {
        // Act
        var result = DateOnlyRange.Create(new DateOnly(2027, 6, 7), default);

        // Assert
        var errors = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        errors.Keys.ToArray().ShouldBe(["endDate"]);
    }

    [Fact]
    public void Equal_ranges_are_equal()
    {
        // Arrange
        var first = DateOnlyRange.Create(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 9)).Value;
        var second = DateOnlyRange.Create(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 9)).Value;

        // Assert
        first.ShouldBe(second);
    }
}
