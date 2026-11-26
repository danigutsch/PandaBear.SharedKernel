namespace SharedKernel.Offerings.Tests;

/// <summary>Tests the call-to-action rule independently of the public offering view.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Offerings")]
public sealed class OfferingCallToActionsTests
{
    [Theory]
    [InlineData(OfferingAvailability.Scheduled, true, OfferingCallToAction.SignUp)]
    [InlineData(OfferingAvailability.Scheduled, false, OfferingCallToAction.Contact)]
    [InlineData(OfferingAvailability.SoldOut, true, OfferingCallToAction.Contact)]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, true, OfferingCallToAction.Contact)]
    [InlineData(OfferingAvailability.OnRequest, true, OfferingCallToAction.Contact)]
    [InlineData(OfferingAvailability.Unspecified, true, OfferingCallToAction.Contact)]
    [InlineData((OfferingAvailability)99, true, OfferingCallToAction.Contact)]
    public void Only_a_scheduled_offering_with_a_sign_up_link_asks_to_sign_up(
        OfferingAvailability availability, bool hasSignUpLink, OfferingCallToAction expected)
    {
        // Act
        var action = OfferingCallToActions.For(availability, hasSignUpLink);

        // Assert
        action.ShouldBe(expected);
    }
}
