namespace SharedKernel.BuildingBlocks.Tests;

public sealed class BuildingBlockTests
{
    [Fact]
    public void Value_objects_are_equal_when_components_match()
    {
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Lisbon", 3);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Value_object_equality_handles_reference_and_type_comparisons()
    {
        var valueObject = new TestValueObject("Lisbon", 3);

        Assert.True(valueObject.Equals(valueObject));
        Assert.False(valueObject.Equals(new OtherTestValueObject("Lisbon", 3)));
    }

    [Fact]
    public void Value_objects_are_not_equal_when_components_differ()
    {
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Porto", 3);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DateRange_create_returns_valid_range_when_end_is_after_start()
    {
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        var result = DateRange.Create(startDate, endDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.DurationDays);
    }

    [Fact]
    public void DateRange_create_rejects_end_dates_before_or_equal_to_start()
    {
        var date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = DateRange.Create(date, date);

        Assert.False(result.IsSuccess);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["End date must be after start date."], error.ValidationErrors["schedule"]);
    }
}
