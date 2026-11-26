namespace SharedKernel.Offerings;

/// <summary>Chooses the public call to action of an offering.</summary>
public static class OfferingCallToActions
{
    /// <summary>Returns sign-up for a scheduled offering with a sign-up link, and contact in every other case.</summary>
    /// <param name="availability">The availability.</param>
    /// <param name="hasSignUpLink">Whether a sign-up link is available.</param>
    /// <returns>The call to action.</returns>
    public static OfferingCallToAction For(OfferingAvailability availability, bool hasSignUpLink) =>
        availability == OfferingAvailability.Scheduled && hasSignUpLink
            ? OfferingCallToAction.SignUp
            : OfferingCallToAction.Contact;
}
