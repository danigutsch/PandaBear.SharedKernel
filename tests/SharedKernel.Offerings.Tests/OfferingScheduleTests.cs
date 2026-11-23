using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the availability table: which dates, year and date note each availability takes, and the duration.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingScheduleTests
{
    private static readonly DateOnly Start = new(2027, 9, 20);
    private static readonly DateOnly End = new(2027, 9, 22);

    [Fact]
    public void A_scheduled_offering_takes_its_year_from_the_first_day_and_has_one_night_fewer_than_days()
    {
        // Act
        var offering = Valid(Tour() with { Schedule = Schedule(OfferingAvailability.Scheduled, new DateOnly(2027, 12, 30), new DateOnly(2028, 1, 1)) });

        // Assert
        offering.Schedule.Availability.ShouldBe(OfferingAvailability.Scheduled);
        offering.Schedule.Dates.ShouldNotBeNull().StartDate.ShouldBe(new DateOnly(2027, 12, 30));
        offering.Schedule.Year.ShouldBe(2027);
        offering.Schedule.DurationDays.ShouldBe(3);
        offering.Schedule.Nights.ShouldBe(2);
    }

    [Theory]
    [InlineData(OfferingAvailability.SoldOut, true, null, 2027)]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, true, null, 2027)]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, false, 2028, 2028)]
    [InlineData(OfferingAvailability.OnRequest, false, null, null)]
    public void Each_availability_accepts_its_dates_and_year(OfferingAvailability availability, bool dated, int? year, int? expectedYear)
    {
        // Arrange
        var schedule = dated ? Schedule(availability, Start, End) : Schedule(availability, year: year);

        // Act
        var offering = Valid(Tour() with { Schedule = schedule });

        // Assert
        offering.Schedule.Availability.ShouldBe(availability);
        var hasDates = offering.Schedule.Dates is not null;
        hasDates.ShouldBe(dated);
        offering.Schedule.Year.ShouldBe(expectedYear);
    }

    [Theory]
    [InlineData((int)OfferingAvailability.Unspecified, "Availability is required.")]
    [InlineData(99, "The availability is not supported.")]
    public void A_missing_or_unknown_availability_is_refused_and_never_means_scheduled(int availability, string message)
    {
        // Act
        var error = Refused(Tour() with { Schedule = Schedule((OfferingAvailability)availability, Start, End) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Availability, message);
    }

    [Fact]
    public void An_unspecified_availability_is_refused_whatever_the_dates()
    {
        // Act
        var dated = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.Unspecified, Start, End) });
        var undated = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.Unspecified, year: 2027) });

        // Assert
        dated.ShouldHaveValidationError(OfferingFields.Availability, "Availability is required.");
        undated.ShouldHaveValidationError(OfferingFields.Availability, "Availability is required.");
    }

    [Theory]
    [InlineData(OfferingAvailability.Scheduled)]
    [InlineData(OfferingAvailability.SoldOut)]
    public void A_scheduled_or_sold_out_offering_needs_its_dates(OfferingAvailability availability)
    {
        // Act
        var error = Refused(Tour() with { Schedule = Schedule(availability, year: 2027) });

        // Assert
        Messages(error, OfferingFields.StartDate).ShouldBe(["A scheduled or sold-out offering needs its dates."]);
    }

    [Fact]
    public void An_offering_on_request_has_neither_dates_nor_a_year()
    {
        // Act
        var dated = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.OnRequest, Start, End) });
        var withYear = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.OnRequest, year: 2027) });

        // Assert
        dated.ShouldHaveValidationError(OfferingFields.StartDate, "An offering on request has no dates.");
        withYear.ShouldHaveValidationError(OfferingFields.Year, "An offering on request has no year.");
    }

    [Fact]
    public void An_undated_offering_whose_dates_are_to_be_confirmed_needs_a_year()
    {
        // Act
        var error = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Year, "An offering whose dates are to be confirmed needs a year until it has dates.");
    }

    [Theory]
    [InlineData(OfferingAvailability.Scheduled)]
    [InlineData(OfferingAvailability.DatesToBeConfirmed)]
    public void A_dated_offering_refuses_a_given_year(OfferingAvailability availability)
    {
        // Act
        var error = Refused(Tour() with { Schedule = Schedule(availability, Start, End, year: 2027) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Year, "The year comes from the dates; leave it empty.");
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void A_year_outside_the_rules_is_refused(int year)
    {
        // Act
        var undated = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed, year: year) });
        var dated = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.Scheduled, new DateOnly(year, 5, 1), new DateOnly(year, 5, 3)) });

        // Assert
        undated.ShouldHaveValidationError(OfferingFields.Year, "Year must be between 2000 and 2100.");
        dated.ShouldHaveValidationError(OfferingFields.StartDate, "The dates must be between 2000 and 2100.");
    }

    [Fact]
    public void Both_dates_or_neither_are_given_and_the_end_is_not_before_the_start()
    {
        // Act
        var startOnly = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed, Start, null) });
        var endOnly = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed, null, End) });
        var reversed = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.Scheduled, End, Start) });

        // Assert
        startOnly.ShouldHaveValidationError(OfferingFields.EndDate, "End date is required when a start date is given.");
        endOnly.ShouldHaveValidationError(OfferingFields.StartDate, "Start date is required when an end date is given.");
        reversed.ShouldHaveValidationError(OfferingFields.EndDate, "End date cannot be before start date.");
    }

    [Theory]
    [InlineData(null, "Duration is required.")]
    [InlineData(0, "Duration must be between 1 and 30 days.")]
    [InlineData(31, "Duration must be between 1 and 30 days.")]
    public void The_duration_is_required_and_bounded_even_without_dates(int? days, string message)
    {
        // Act
        var error = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.OnRequest, days: days), Steps = [] });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.DurationDays, message);
    }

    [Fact]
    public void The_duration_of_a_dated_offering_counts_the_first_and_the_last_day()
    {
        // Act
        var nights = Refused(Tour() with { Schedule = Schedule(OfferingAvailability.Scheduled, Start, End, days: 2), Steps = [] });

        // Assert
        nights.ShouldHaveValidationError(OfferingFields.DurationDays, "Duration must equal the number of days from the start to the end date, both included.");
    }

    [Fact]
    public void A_one_day_offering_has_no_night()
    {
        // Act
        var offering = Valid(Tour() with { Schedule = Schedule(OfferingAvailability.Scheduled, Start, Start, days: 1), Steps = [], Translations = [Translation() with { StepTexts = null }] });

        // Assert
        offering.Schedule.DurationDays.ShouldBe(1);
        offering.Schedule.Nights.ShouldBe(0);
    }

    [Theory]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, 2028)]
    [InlineData(OfferingAvailability.OnRequest, null)]
    public void A_date_note_is_allowed_only_without_dates(OfferingAvailability availability, int? year)
    {
        // Arrange
        var note = Translation() with { DateNote = "Carnaval" };

        // Act
        var undated = Valid(Tour(note) with { Schedule = Schedule(availability, year: year) });
        var dated = Refused(Tour(note) with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed, Start, End) });

        // Assert
        undated.Translations[0].DateNote.ShouldBe("Carnaval");
        dated.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.DateNote), "A date note is allowed only when the offering has no dates.");
    }

    [Fact]
    public void A_long_date_note_is_refused()
    {
        // Act
        var error = Refused(Tour(Translation() with { DateNote = new string('x', 101) }) with { Schedule = Schedule(OfferingAvailability.OnRequest) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.DateNote), "Date note cannot exceed 100 characters.");
    }
}
