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
    public void Value_objects_are_not_equal_when_later_components_differ()
    {
        // Arrange
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Lisbon", 4);

        // Act
        var areEqual = first.Equals(second);

        // Assert
        Assert.False(areEqual);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("null", "null", true)]
    [InlineData("value", "null", false)]
    [InlineData("null", "value", false)]
    public void Value_object_operators_handle_null_values(string leftCase, string rightCase, bool expectedEqual)
    {
        // Arrange
        var left = CreateValueObject(leftCase);
        var right = CreateValueObject(rightCase);

        // Act
        var areEqual = AreEqual(left, right);
        var areNotEqual = AreNotEqual(left, right);

        // Assert
        if (expectedEqual)
        {
            Assert.True(areEqual);
            Assert.False(areNotEqual);
        }
        else
        {
            Assert.False(areEqual);
            Assert.True(areNotEqual);
        }

        static bool AreEqual(ValueObject? first, ValueObject? second) => first == second;
        static bool AreNotEqual(ValueObject? first, ValueObject? second) => first != second;
        static ValueObject? CreateValueObject(string valueCase) => valueCase == "null" ? null : new TestValueObject("Lisbon", 3);
    }

    [Fact]
    public void DateRange_create_returns_valid_range_when_end_is_after_start()
    {
        // Arrange
        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(startDate, result.Value.StartDate);
        Assert.Equal(endDate, result.Value.EndDate);
        Assert.Equal(7, result.Value.DurationDays);
    }

    [Fact]
    public void DateRange_create_rejects_end_dates_equal_to_start()
    {
        // Arrange
        var date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        Assert.False(result.IsSuccess);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["End date must be after start date."], error.ValidationErrors["schedule"]);
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
        Assert.False(result.IsSuccess);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["End date must be after start date."], error.ValidationErrors["schedule"]);
    }
}
