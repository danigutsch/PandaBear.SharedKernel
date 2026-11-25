using System.Globalization;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the culture-aware display of a range of days with patterns chosen by the shared month and year.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class DateOnlyRangeDisplayTests
{
    private static readonly DateRangePatterns Portuguese = new(
        singleDay: "{0:d 'de' MMMM 'de' yyyy}",
        sameMonth: "{0:%d} a {1:d 'de' MMMM 'de' yyyy}",
        sameYear: "{0:d 'de' MMMM} a {1:d 'de' MMMM 'de' yyyy}",
        other: "{0:d 'de' MMMM 'de' yyyy} a {1:d 'de' MMMM 'de' yyyy}");

    private static readonly DateRangePatterns English = new(
        singleDay: "{0:d MMMM yyyy}",
        sameMonth: "{0:%d}–{1:d MMMM yyyy}",
        sameYear: "{0:d MMMM} – {1:d MMMM yyyy}",
        other: "{0:d MMMM yyyy} – {1:d MMMM yyyy}");

    [Theory]
    [InlineData(2027, 9, 20, 2027, 9, 29, "20 a 29 de setembro de 2027")]
    [InlineData(2027, 9, 28, 2027, 10, 5, "28 de setembro a 5 de outubro de 2027")]
    [InlineData(2027, 12, 28, 2028, 1, 4, "28 de dezembro de 2027 a 4 de janeiro de 2028")]
    [InlineData(2027, 9, 20, 2027, 9, 20, "20 de setembro de 2027")]
    public void Portuguese_ranges_use_the_pattern_of_their_shared_month_and_year(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay, string expected)
    {
        // Arrange
        var range = DateOnlyRange.Create(new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay)).ShouldBeSuccess();

        // Act
        var display = range.ToDisplayString(Portuguese, CultureInfo.GetCultureInfo("pt-BR"));

        // Assert
        display.ShouldBe(expected);
    }

    [Theory]
    [InlineData(2027, 9, 20, 2027, 9, 29, "20–29 September 2027")]
    [InlineData(2027, 9, 28, 2027, 10, 5, "28 September – 5 October 2027")]
    [InlineData(2027, 12, 28, 2028, 1, 4, "28 December 2027 – 4 January 2028")]
    [InlineData(2027, 9, 20, 2027, 9, 20, "20 September 2027")]
    public void English_ranges_use_the_pattern_of_their_shared_month_and_year(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay, string expected)
    {
        // Arrange
        var range = DateOnlyRange.Create(new DateOnly(startYear, startMonth, startDay), new DateOnly(endYear, endMonth, endDay)).ShouldBeSuccess();

        // Act
        var display = range.ToDisplayString(English, CultureInfo.GetCultureInfo("en"));

        // Assert
        display.ShouldBe(expected);
    }

    [Fact]
    public void The_same_month_of_another_year_is_not_the_same_month()
    {
        // Arrange
        var range = DateOnlyRange.Create(new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 2)).ShouldBeSuccess();

        // Act
        var display = range.ToDisplayString(Portuguese, CultureInfo.GetCultureInfo("pt-BR"));

        // Assert
        display.ShouldBe("20 de setembro de 2027 a 2 de setembro de 2028");
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        // Arrange
        var range = DateOnlyRange.Create(new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 29)).ShouldBeSuccess();

        // Act
        Action noPatterns = () => range.ToDisplayString(null!, CultureInfo.InvariantCulture);
        Action noProvider = () => range.ToDisplayString(English, null!);

        // Assert
        noPatterns.ShouldThrow<ArgumentNullException>();
        noProvider.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(" ", "b", "c", "d")]
    [InlineData("a", " ", "c", "d")]
    [InlineData("a", "b", "", "d")]
    [InlineData("a", "b", "c", "\t")]
    public void A_blank_pattern_is_refused(string singleDay, string sameMonth, string sameYear, string other)
    {
        // Act
        Action act = () => _ = new DateRangePatterns(singleDay, sameMonth, sameYear, other);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_missing_pattern_is_refused()
    {
        // Act
        Action act = () => _ = new DateRangePatterns("a", "b", "c", null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void The_patterns_are_kept()
    {
        // Act
        var patterns = new DateRangePatterns("a", "b", "c", "d");

        // Assert
        patterns.SingleDay.ShouldBe("a");
        patterns.SameMonth.ShouldBe("b");
        patterns.SameYear.ShouldBe("c");
        patterns.Other.ShouldBe("d");
    }
}
