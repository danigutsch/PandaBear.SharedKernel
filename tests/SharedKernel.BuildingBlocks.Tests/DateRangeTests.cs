using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

public sealed class DateRangeTests
{
    [Fact]
    public void Create_withvaliddates_returnssuccessresult()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 10);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.NotNull(result.Value);
        TestAssert.Equal(startDate, result.Value.StartDate);
        TestAssert.Equal(endDate, result.Value.EndDate);
    }

    [Fact]
    public void Create_withenddatebeforestartdate_returnsinvalidresult()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 10);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.False(result.IsSuccess);
        TestAssert.True(result.IsFailure);
        TestAssert.Equal(ResultStatus.Invalid, result.Status);
        TestAssert.NotNull(result.ErrorDetails);
        TestAssert.Equal("End date must be after start date.", result.ErrorDetails.Detail);
        TestAssert.NotNull(result.ErrorDetails.ValidationErrors);
        TestAssert.Contains("schedule", result.ErrorDetails.ValidationErrors.Keys);
        TestAssert.Equal(["End date must be after start date."], result.ErrorDetails.ValidationErrors["schedule"]);
    }

    [Fact]
    public void Create_withenddateequaltostartdate_returnsinvalidresult()
    {
        // Arrange
        var date = DateRangeTestsHelpers.UtcDate(2025, 6, 1);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        TestAssert.False(result.IsSuccess);
        TestAssert.True(result.IsFailure);
        TestAssert.Equal(ResultStatus.Invalid, result.Status);
        TestAssert.NotNull(result.ErrorDetails);
        TestAssert.Equal("End date must be after start date.", result.ErrorDetails.Detail);
    }

    [Fact]
    public void DurationDays_calculatescorrectduration()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 8);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(7.0, result.Value.DurationDays);
    }

    [Fact]
    public void DurationDays_withsingleday_returnscorrectvalue()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 2);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(1.0, result.Value.DurationDays);
    }

    [Fact]
    public void DurationDays_withpartialdays_returnsdecimalvalue()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1, 10);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 2, 14);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(28.0 / 24.0, result.Value.DurationDays, precision: 10);
    }

    [Fact]
    public void Equality_withsamedates_areequal()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 6, 10);
        var range1 = DateRange.Create(startDate, endDate).Value;
        var range2 = DateRange.Create(startDate, endDate).Value;

        // Act
        // Assert
        TestAssert.Equal(range1, range2);
        TestAssert.True(range1.Equals(range2));
    }

    [Fact]
    public void Equality_withdifferentdates_arenotequal()
    {
        // Arrange
        var range1 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 6, 1), DateRangeTestsHelpers.UtcDate(2025, 6, 10)).Value;
        var range2 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 7, 1), DateRangeTestsHelpers.UtcDate(2025, 7, 10)).Value;

        // Act
        // Assert
        TestAssert.NotEqual(range1, range2);
        TestAssert.False(range1.Equals(range2));
    }

    [Fact]
    public void Create_withutcdates_preservesutckind()
    {
        // Arrange
        var startDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(DateTimeKind.Utc, result.Value.StartDate.Kind);
        TestAssert.Equal(DateTimeKind.Utc, result.Value.EndDate.Kind);
    }

    [Fact]
    public void Create_withlongduration_calculatescorrectly()
    {
        // Arrange
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 1, 1);
        var endDate = DateRangeTestsHelpers.UtcDate(2025, 12, 31);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(364.0, result.Value.DurationDays);
    }

    [Fact]
    public void Create_withminimumtimespan_returnssuccess()
    {
        // Arrange
        const int oneSecondDifference = 1;
        var startDate = DateRangeTestsHelpers.UtcDate(2025, 6, 1, 12);
        var endDate = startDate.AddSeconds(oneSecondDifference);

        // Act
        var result = DateRange.Create(startDate, endDate);

        // Assert
        TestAssert.True(result.IsSuccess);
        TestAssert.True(result.Value.DurationDays > 0);
        TestAssert.True(result.Value.DurationDays < 0.001);
    }

}
