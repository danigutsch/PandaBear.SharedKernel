namespace SharedKernel.HttpClients;

/// <summary>
/// Describes the HTTP outcome of a command request without applying caller fallback policy.
/// </summary>
public enum ContractCommandOutcomeKind
{
    /// <summary>
    /// The command completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The API returned a validation problem response.
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
    /// The API returned a response body that could not be deserialized into the expected contract.
    /// </summary>
    MalformedBody,

    /// <summary>
    /// The API returned a status code not explicitly modeled by this client contract.
    /// </summary>
    UnexpectedStatus
}
