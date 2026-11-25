namespace SharedKernel.Offerings;

/// <summary>What the public page of an offering asks the reader to do. The default value, <see cref="Unspecified"/>, means "not set".</summary>
public enum OfferingCallToAction
{
    /// <summary>
    /// Not set. It is never the call to action of a view, which is always <see cref="SignUp"/> or <see cref="Contact"/>. It is the zero
    /// value so that an unset field or an empty payload is never read as a real call to action.
    /// </summary>
    Unspecified = 0,

    /// <summary>Sign up through the offering's sign-up link: a scheduled offering that has one.</summary>
    SignUp = 1,

    /// <summary>Contact the provider, through the application's contact channel.</summary>
    Contact = 2,
}
