namespace SharedKernel.BuildingBlocks.Tests;

public sealed class BuildingBlockTests
{
    [Fact]
    public void Value_objects_are_equal_when_components_match()
    {
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Lisbon", 3);

        TestAssert.Equal(first, second);
        TestAssert.True(first == second);
        TestAssert.False(first != second);
        TestAssert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Value_object_equality_handles_reference_and_type_comparisons()
    {
        var valueObject = new TestValueObject("Lisbon", 3);

        TestAssert.True(valueObject.Equals(valueObject));
        TestAssert.False(valueObject.Equals(new OtherTestValueObject("Lisbon", 3)));
    }

    [Fact]
    public void Value_objects_are_not_equal_when_components_differ()
    {
        var first = new TestValueObject("Lisbon", 3);
        var second = new TestValueObject("Porto", 3);

        TestAssert.NotEqual(first, second);
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
        TestAssert.False(areEqual);
        TestAssert.NotEqual(first, second);
    }

    [Fact]
    public void Value_object_operators_treat_two_null_values_as_equal()
    {
        // Arrange
        ValueObject?[] operands = [null, null];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        TestAssert.True(areEqual);
        TestAssert.False(areNotEqual);
    }

    [Fact]
    public void Value_object_operators_treat_left_value_and_right_null_as_not_equal()
    {
        // Arrange
        ValueObject?[] operands = [new TestValueObject("Lisbon", 3), null];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        TestAssert.False(areEqual);
        TestAssert.True(areNotEqual);
    }

    [Fact]
    public void Value_object_operators_treat_left_null_and_right_value_as_not_equal()
    {
        // Arrange
        ValueObject?[] operands = [null, new TestValueObject("Lisbon", 3)];
        var left = operands[0];
        var right = operands[1];

        // Act
        var areEqual = left == right;
        var areNotEqual = left != right;

        // Assert
        TestAssert.False(areEqual);
        TestAssert.True(areNotEqual);
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
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(startDate, result.Value.StartDate);
        TestAssert.Equal(endDate, result.Value.EndDate);
        TestAssert.Equal(7, result.Value.DurationDays);
    }

    [Fact]
    public void DateRange_create_rejects_end_dates_equal_to_start()
    {
        // Arrange
        var date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        TestAssert.False(result.IsSuccess);
        var error = result.ErrorDetails;
        _ = TestAssert.NotNull(error);
        TestAssert.NotNull(error.ValidationErrors);
        TestAssert.True(error.ValidationErrors.ContainsKey("schedule"));
        TestAssert.Equal(["End date must be after start date."], error.ValidationErrors["schedule"]);
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
        TestAssert.False(result.IsSuccess);
        var error = result.ErrorDetails;
        _ = TestAssert.NotNull(error);
        TestAssert.NotNull(error.ValidationErrors);
        TestAssert.True(error.ValidationErrors.ContainsKey("schedule"));
        TestAssert.Equal(["End date must be after start date."], error.ValidationErrors["schedule"]);
    }
}
