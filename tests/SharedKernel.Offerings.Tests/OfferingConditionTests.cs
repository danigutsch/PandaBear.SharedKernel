using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the group sizes, the minimum-group condition and the payment terms.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingConditionTests
{
    [Fact]
    public void Valid_conditions_are_kept()
    {
        // Act
        var offering = Valid(Tour() with
        {
            Participants = new ParticipantRange(6, 16),
            GroupCondition = new GroupConditionDefinition(8, 150m, 45),
            PaymentTerms = new PaymentTerms(30, 10, 60),
        });

        // Assert
        offering.Participants.ShouldBe(new ParticipantRange(6, 16));
        offering.GroupCondition.ShouldBe(new GroupCondition(8, Money.Restore(150m, Currency.Eur), 45));
        offering.PaymentTerms.ShouldBe(new PaymentTerms(30, 10, 60));
    }

    [Fact]
    public void Empty_conditions_are_none()
    {
        // Act
        var offering = Valid(Tour() with
        {
            Participants = new ParticipantRange(null, null),
            GroupCondition = new GroupConditionDefinition(null, null, null),
            PaymentTerms = new PaymentTerms(null, null, null),
        });

        // Assert
        offering.Participants.ShouldBeNull();
        offering.GroupCondition.ShouldBeNull();
        offering.PaymentTerms.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 10, OfferingFields.ParticipantsMin, "Smallest group must be between 1 and 40.")]
    [InlineData(41, null, OfferingFields.ParticipantsMin, "Smallest group must be between 1 and 40.")]
    [InlineData(null, 41, OfferingFields.ParticipantsMax, "Largest group must be between 1 and 40.")]
    [InlineData(10, 6, OfferingFields.ParticipantsMax, "The largest group cannot be smaller than the smallest.")]
    public void Group_sizes_are_bounded_and_ordered(int? min, int? max, string field, string message)
    {
        // Act
        var error = Refused(Tour() with { Participants = new ParticipantRange(min, max) });

        // Assert
        error.ShouldHaveValidationError(field, message);
    }

    [Fact]
    public void The_group_condition_takes_all_three_values_together()
    {
        // Act
        var error = Refused(Tour() with { GroupCondition = new GroupConditionDefinition(8, null, null) });

        // Assert
        Fields(error).ShouldBe([OfferingFields.GroupConditionDueDays, OfferingFields.GroupConditionSurchargePerMissing]);
        error.ShouldHaveValidationError(OfferingFields.GroupConditionSurchargePerMissing, "Give the minimum participants, the surcharge per missing participant and the due days together.");
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(17, 16)]
    [InlineData(41, null)]
    public void The_minimum_is_one_to_the_largest_group_or_the_rules_maximum(int minimum, int? largest)
    {
        // Act
        var error = Refused(Tour() with { Participants = new ParticipantRange(null, largest), GroupCondition = new(minimum, 150m, 45) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.GroupConditionMinimumParticipants, "Minimum participants must be between 1 and the largest group size.");
    }

    [Fact]
    public void The_minimum_is_separate_from_the_smallest_group()
    {
        // Act
        var offering = Valid(Tour() with { Participants = new ParticipantRange(10, 16), GroupCondition = new(6, 150m, 45) });

        // Assert
        offering.GroupCondition.ShouldNotBeNull().MinimumParticipants.ShouldBe(6);
    }

    [Theory]
    [InlineData("0", "Surcharge must be greater than zero.")]
    [InlineData("50000.01", "Surcharge cannot exceed 50000.")]
    [InlineData("1.001", "Surcharge must have at most 2 decimal places, like 1500 or 1500.50.")]
    public void The_surcharge_is_positive_money_in_the_offering_currency(string surcharge, string message)
    {
        // Act
        var error = Refused(Tour() with { GroupCondition = new(8, decimal.Parse(surcharge, System.Globalization.CultureInfo.InvariantCulture), 45) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.GroupConditionSurchargePerMissing, message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void The_due_days_are_one_to_a_year(int dueDays)
    {
        // Act
        var error = Refused(Tour() with { GroupCondition = new(8, 150m, dueDays) });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.GroupConditionDueDays, "Due days must be between 1 and 365.");
    }

    [Theory]
    [InlineData(0, null, null, OfferingFields.PaymentTermsDepositPercent, "Deposit must be between 1 and 100 percent.")]
    [InlineData(101, null, null, OfferingFields.PaymentTermsDepositPercent, "Deposit must be between 1 and 100 percent.")]
    [InlineData(null, 0, null, OfferingFields.PaymentTermsInstalments, "Instalments must be between 1 and 24.")]
    [InlineData(null, 25, null, OfferingFields.PaymentTermsInstalments, "Instalments must be between 1 and 24.")]
    [InlineData(null, null, 0, OfferingFields.PaymentTermsBalanceDueDays, "Balance due days must be between 1 and 365.")]
    [InlineData(null, null, 366, OfferingFields.PaymentTermsBalanceDueDays, "Balance due days must be between 1 and 365.")]
    public void Each_payment_term_is_optional_and_bounded(int? deposit, int? instalments, int? balance, string field, string message)
    {
        // Act
        var error = Refused(Tour() with { PaymentTerms = new PaymentTerms(deposit, instalments, balance) });

        // Assert
        error.ShouldHaveValidationError(field, message);
    }

    [Fact]
    public void A_single_payment_term_is_kept()
    {
        // Act
        var offering = Valid(Tour() with { PaymentTerms = new PaymentTerms(null, 12, null) });

        // Assert
        offering.PaymentTerms.ShouldBe(new PaymentTerms(null, 12, null));
    }
}
