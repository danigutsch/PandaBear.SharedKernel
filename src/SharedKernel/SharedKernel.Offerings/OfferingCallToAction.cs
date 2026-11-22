using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Offerings;

/// <summary>What the public page of an offering asks the reader to do. Zero is not defined, so an unset value is never a call to action.</summary>
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Zero is deliberately undefined, like OfferingAvailability, so an unset value never reads as SignUp.")]
public enum OfferingCallToAction
{
    /// <summary>Sign up through the offering's sign-up link: a scheduled offering that has one.</summary>
    SignUp = 1,

    /// <summary>Contact the provider, through the application's contact channel.</summary>
    Contact = 2,
}
