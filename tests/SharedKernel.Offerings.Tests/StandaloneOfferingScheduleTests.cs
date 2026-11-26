using static SharedKernel.Offerings.Tests.OfferingFixtures;

namespace SharedKernel.Offerings.Tests;

/// <summary>Tests the schedule policy when a consumer owns its own aggregate.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Offerings")]
public sealed class StandaloneOfferingScheduleTests
{
    [Fact]
    public void A_valid_schedule_matches_the_offering_schedule()
    {
        // Arrange
        var definition = Tour() with
        {
            Schedule = Schedule(OfferingAvailability.Scheduled, new DateOnly(2027, 12, 30), new DateOnly(2028, 1, 1)),
        };
        var expected = Valid(definition).Schedule;

        // Act
        var result = OfferingSchedules.Create(definition.Schedule, Rules.MinYear, Rules.MaxYear, Rules.MaxDurationDays);

        // Assert
        result.ShouldBeSuccess().ShouldBe(expected);
    }

    [Fact]
    public void The_schedule_exposes_the_same_field_messages_as_the_offering()
    {
        // Arrange
        var definition = Tour() with
        {
            Schedule = Schedule(OfferingAvailability.OnRequest, new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 22), year: 2027, days: null),
        };
        var expected = Refused(definition);

        // Act
        var result = OfferingSchedules.Create(definition.Schedule, Rules.MinYear, Rules.MaxYear, Rules.MaxDurationDays);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.StartDate, "An offering on request has no dates.");
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.Year,
            "An offering on request has no year.", "The year comes from the dates; leave it empty.");
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.DurationDays, "Duration is required.");
        Fields(result.ErrorDetails).ShouldBe(Fields(expected));
        Messages(result.ErrorDetails, OfferingFields.Year).ShouldBe(Messages(expected, OfferingFields.Year));
    }

    [Fact]
    public void Standalone_schedule_limits_apply_to_both_dates_and_explicit_duration()
    {
        // Arrange
        var definition = Schedule(OfferingAvailability.SoldOut, new DateOnly(2027, 12, 30), new DateOnly(2028, 1, 1));

        // Act
        var result = OfferingSchedules.Create(definition, minYear: 2027, maxYear: 2027, maxDurationDays: 2);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.StartDate, "The dates must be between 2027 and 2027.");
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.DurationDays, "Duration must be between 1 and 2 days.");
    }

    [Fact]
    public void An_undated_schedule_keeps_the_entered_year_and_duration()
    {
        // Arrange
        var definition = Schedule(OfferingAvailability.DatesToBeConfirmed, year: 2028, days: 7);

        // Act
        var result = OfferingSchedules.Create(definition, Rules.MinYear, Rules.MaxYear, Rules.MaxDurationDays);

        // Assert
        result.ShouldBeSuccess().ShouldBe(new OfferingSchedule(OfferingAvailability.DatesToBeConfirmed, null, 2028, 7));
    }
}
