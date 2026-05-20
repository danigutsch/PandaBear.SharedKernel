namespace SharedKernel.Functional;

/// <summary>
/// Represents the status of a result.
/// </summary>
public enum ResultStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Ok = 200,

    /// <summary>
    /// Resource was created successfully.
    /// </summary>
    Created = 201,

    /// <summary>
    /// The request has been accepted for processing.
    /// </summary>
    Accepted = 202,

    /// <summary>
    /// No content to return.
    /// </summary>
    NoContent = 204,

    /// <summary>
    /// The request was invalid.
    /// </summary>
    Invalid = 400,

    /// <summary>
    /// Unauthorized access to the resource.
    /// </summary>
    Unauthorized = 401,

    /// <summary>
    /// Access to the resource is forbidden.
    /// </summary>
    Forbidden = 403,

    /// <summary>
    /// The requested resource was not found.
    /// </summary>
    NotFound = 404,

    /// <summary>
    /// There was a conflict with the current state of the resource.
    /// </summary>
    Conflict = 409,

    /// <summary>
    /// An error occurred during the operation.
    /// </summary>
    Error = 422,

    /// <summary>
    /// A critical error occurred.
    /// </summary>
    CriticalError = 500,

    /// <summary>
    /// The resource is unavailable.
    /// </summary>
    Unavailable = 503,
}
