namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the validated date of birth.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "ValueObjects")]
public sealed class BirthDateTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);

    [Fact]
    public void A_date_within_the_age_limits_is_accepted()
    {
        // Act
        var birthDate = BirthDate.Create(new DateOnly(1990, 5, 20), Today, minAgeYears: 10, maxAgeYears: 120).ShouldBeSuccess();

        // Assert
        birthDate.Value.ShouldBe(new DateOnly(1990, 5, 20));
        birthDate.AgeOn(Today).ShouldBe(36);
    }

    [Theory]
    [InlineData(2016, 11, 5)]
    [InlineData(1906, 11, 5)]
    public void The_exact_age_limits_are_accepted_on_the_birthday(int year, int month, int day)
    {
        // Act
        var result = BirthDate.Create(new DateOnly(year, month, day), Today, 10, 120);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_missing_date_is_reported_as_required()
    {
        // Act
        var result = BirthDate.Create(default, Today, 10, 120);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["birthDate"].ShouldBe(["Birth date is required."]);
    }

    [Theory]
    [InlineData(2026, 11, 6, "Birth date cannot be in the future.")]
    [InlineData(2016, 11, 6, "Age must be at least 10 years.")]
    [InlineData(1905, 11, 5, "Age cannot exceed 120 years.")]
    public void A_date_outside_the_limits_is_rejected(int year, int month, int day, string message)
    {
        // Act
        var result = BirthDate.Create(new DateOnly(year, month, day), Today, 10, 120);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["birthDate"].ShouldBe([message]);
    }

    [Theory]
    [InlineData(2026, 2, 28, 17)]
    [InlineData(2026, 3, 1, 18)]
    [InlineData(2027, 2, 28, 18)]
    [InlineData(2028, 2, 28, 19)]
    [InlineData(2028, 2, 29, 20)]
    public void A_leap_day_birthday_is_reached_on_march_first_in_non_leap_years(int year, int month, int day, int expectedAge)
    {
        // Arrange
        var birthDate = BirthDate.Create(new DateOnly(2008, 2, 29), new DateOnly(2026, 3, 1), 10, 120).ShouldBeSuccess();

        // Act
        var age = birthDate.AgeOn(new DateOnly(year, month, day));

        // Assert
        age.ShouldBe(expectedAge);
    }

    [Theory]
    [InlineData(2026, 2, 27, 17)]
    [InlineData(2026, 3, 1, 18)]
    [InlineData(2026, 3, 2, 18)]
    [InlineData(2026, 12, 31, 18)]
    public void Ordinary_birthdays_are_unchanged(int year, int month, int day, int expectedAge)
    {
        // Arrange
        var birthDate = BirthDate.Create(new DateOnly(2008, 3, 1), new DateOnly(2026, 3, 1), 10, 120).ShouldBeSuccess();

        // Act
        var age = birthDate.AgeOn(new DateOnly(year, month, day));

        // Assert
        age.ShouldBe(expectedAge);
    }

    [Fact]
    public void The_minimum_age_check_rejects_a_leap_day_birth_on_february_28_and_accepts_it_on_march_first()
    {
        // Arrange
        var born = new DateOnly(2008, 2, 29);

        // Act
        var onFebruary = BirthDate.Create(born, new DateOnly(2026, 2, 28), 18, 120);
        var onMarch = BirthDate.Create(born, new DateOnly(2026, 3, 1), 18, 120);

        // Assert
        onFebruary.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["birthDate"].ShouldBe(["Age must be at least 18 years."]);
        onMarch.IsSuccess.ShouldBeTrue();
    }
}
