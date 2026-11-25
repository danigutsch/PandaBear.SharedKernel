namespace SharedKernel.HttpClients;

/// <summary>
/// Describes an HTTP request outcome without applying caller fallback or user-interface policy.
/// </summary>
public enum HttpOutcomeKind
{
    /// <summary>
    /// The response matched the expected success status and, when required, contained a valid JSON value.
    /// </summary>
    Succeeded,

    /// <summary>
    /// An HTTP 400 response contained usable field or model validation errors.
    /// </summary>
    ValidationProblem,

    /// <summary>
    /// The API returned not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// The API returned unauthorized.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The API returned forbidden.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The API returned conflict.
    /// </summary>
    Conflict,

    /// <summary>
    /// The API returned a response that required a body but the body was empty.
    /// </summary>
    EmptyBody,

    /// <summary>
    /// The response body exceeded its read limit, used an unsupported encoding, or failed decoding or validation against the expected contract.
    /// </summary>
    MalformedBody,

    /// <summary>
    /// The API returned a status code not explicitly modeled by this client contract.
    /// </summary>
    UnexpectedStatus,

    /// <summary>
    /// A required JSON response had a missing or non-JSON Content-Type header.
    /// </summary>
    UnsupportedContentType,

    /// <summary>
    /// A timeout interrupted the request or asynchronous response-body processing.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The request transport, circuit breaker, or response-body I/O failed.
    /// </summary>
    TransportFailure
}
