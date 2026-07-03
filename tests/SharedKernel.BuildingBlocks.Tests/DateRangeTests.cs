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
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(startDate, result.Value.StartDate);
        Assert.Equal(endDate, result.Value.EndDate);
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
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.NotNull(result.ErrorDetails);
        Assert.Equal("End date must be after start date.", result.ErrorDetails.Detail);
        Assert.NotNull(result.ErrorDetails.ValidationErrors);
        Assert.Contains("schedule", result.ErrorDetails.ValidationErrors.Keys);
        Assert.Equal(["End date must be after start date."], result.ErrorDetails.ValidationErrors["schedule"]);
    }

    [Fact]
    public void Create_withenddateequaltostartdate_returnsinvalidresult()
    {
        // Arrange
        var date = DateRangeTestsHelpers.UtcDate(2025, 6, 1);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.NotNull(result.ErrorDetails);
        Assert.Equal("End date must be after start date.", result.ErrorDetails.Detail);
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
        Assert.True(result.IsSuccess);
        Assert.Equal(7.0, result.Value.DurationDays);
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
        Assert.True(result.IsSuccess);
        Assert.Equal(1.0, result.Value.DurationDays);
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
        Assert.True(result.IsSuccess);
        Assert.Equal(28.0 / 24.0, result.Value.DurationDays, precision: 10);
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
        Assert.Equal(range1, range2);
        Assert.True(range1.Equals(range2));
    }

    [Fact]
    public void Equality_withdifferentdates_arenotequal()
    {
        // Arrange
        var range1 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 6, 1), DateRangeTestsHelpers.UtcDate(2025, 6, 10)).Value;
        var range2 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 7, 1), DateRangeTestsHelpers.UtcDate(2025, 7, 10)).Value;

        // Act
        // Assert
        Assert.NotEqual(range1, range2);
        Assert.False(range1.Equals(range2));
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
        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeKind.Utc, result.Value.StartDate.Kind);
        Assert.Equal(DateTimeKind.Utc, result.Value.EndDate.Kind);
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
        Assert.True(result.IsSuccess);
        Assert.Equal(364.0, result.Value.DurationDays);
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
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.DurationDays > 0);
        Assert.True(result.Value.DurationDays < 0.001);
    }

}
