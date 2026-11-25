namespace SharedKernel.AspNetCore;

/// <summary>What an error page tells the visitor about a failed response; <see cref="ErrorPageKinds.Of" /> names it for a status code.</summary>
public enum ErrorPageKind
{
    /// <summary>The address has no page (404).</summary>
    NotFound,

    /// <summary>
    /// A client error that the same request cannot fix by trying again, such as a wrong method, so the page does not read as a fault of
    /// the site.
    /// </summary>
    RequestError,

    /// <summary>Something went wrong and the visitor can try again in a moment: a timeout (408), too many requests (429) and every server error.</summary>
    Failure,
}
