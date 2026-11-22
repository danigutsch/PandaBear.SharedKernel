namespace PandaBear.Offerings;

/// <summary>What the public page of an offering asks the reader to do.</summary>
public enum OfferingCallToAction
{
    /// <summary>Sign up through the offering's sign-up link: a scheduled offering that has one.</summary>
    SignUp = 0,

    /// <summary>Contact the provider, through the application's contact channel.</summary>
    Contact = 1,
}
