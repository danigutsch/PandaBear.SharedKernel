namespace PandaBear.AspNetCore;

/// <summary>Names the bounded authentication event and reason values that the security-event reporter and its producers agree on.</summary>
internal static class AuthenticationEventNames
{
    /// <summary>The event tag value for a failed token validation.</summary>
    public const string AuthenticationFailed = "authentication_failed";

    /// <summary>The event tag value for a challenge response.</summary>
    public const string Challenge = "challenge";

    /// <summary>The event tag value for a forbidden response.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The reason for a challenge that carried no token.</summary>
    public const string MissingToken = "missing_token";
}
