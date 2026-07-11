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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value).ShouldNotBeNull();
        (result.Value.StartDate).ShouldBe(startDate);
        (result.Value.EndDate).ShouldBe(endDate);
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
        (result.IsSuccess).ShouldBeFalse();
        (result.IsFailure).ShouldBeTrue();
        (result.Status).ShouldBe(ResultStatus.Invalid);
        (result.ErrorDetails).ShouldNotBeNull();
        (result.ErrorDetails.Detail).ShouldBe("End date must be after start date.");
        (result.ErrorDetails.ValidationErrors).ShouldNotBeNull();
        (result.ErrorDetails.ValidationErrors.Keys).ShouldContain("schedule");
        (result.ErrorDetails.ValidationErrors["schedule"]).ShouldBe(["End date must be after start date."]);
    }

    [Fact]
    public void Create_withenddateequaltostartdate_returnsinvalidresult()
    {
        // Arrange
        var date = DateRangeTestsHelpers.UtcDate(2025, 6, 1);

        // Act
        var result = DateRange.Create(date, date);

        // Assert
        (result.IsSuccess).ShouldBeFalse();
        (result.IsFailure).ShouldBeTrue();
        (result.Status).ShouldBe(ResultStatus.Invalid);
        (result.ErrorDetails).ShouldNotBeNull();
        (result.ErrorDetails.Detail).ShouldBe("End date must be after start date.");
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.DurationDays).ShouldBe(7.0);
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.DurationDays).ShouldBe(1.0);
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.DurationDays).ShouldBe(28.0 / 24.0, precision: 10);
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
        (range2).ShouldBe(range1);
        (range1.Equals(range2)).ShouldBeTrue();
    }

    [Fact]
    public void Equality_withdifferentdates_arenotequal()
    {
        // Arrange
        var range1 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 6, 1), DateRangeTestsHelpers.UtcDate(2025, 6, 10)).Value;
        var range2 = DateRange.Create(DateRangeTestsHelpers.UtcDate(2025, 7, 1), DateRangeTestsHelpers.UtcDate(2025, 7, 10)).Value;

        // Act
        // Assert
        (range2).ShouldNotBe(range1);
        (range1.Equals(range2)).ShouldBeFalse();
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.StartDate.Kind).ShouldBe(DateTimeKind.Utc);
        (result.Value.EndDate.Kind).ShouldBe(DateTimeKind.Utc);
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.DurationDays).ShouldBe(364.0);
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
        (result.IsSuccess).ShouldBeTrue();
        (result.Value.DurationDays > 0).ShouldBeTrue();
        (result.Value.DurationDays < 0.001).ShouldBeTrue();
    }

}
