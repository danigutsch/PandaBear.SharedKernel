namespace SharedKernel.Functional;

/// <summary>
/// Stable machine-readable error codes for result failures.
/// </summary>
public static class ResultErrorCodes
{
    /// <summary>
    /// Generic invalid request or validation failure.
    /// </summary>
    public const string Invalid = "invalid";

    /// <summary>
    /// Requested resource was not found.
    /// </summary>
    public const string NotFound = "not_found";

    /// <summary>
    /// Caller is not authenticated.
    /// </summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>
    /// Caller is authenticated but not allowed.
    /// </summary>
    public const string Forbidden = "forbidden";

    /// <summary>
    /// Current resource state conflicts with the requested change.
    /// </summary>
    public const string Conflict = "conflict";

    /// <summary>
    /// Generic processing failure.
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// Critical unrecoverable failure.
    /// </summary>
    public const string CriticalError = "critical_error";

    /// <summary>
    /// Required dependency or service is unavailable.
    /// </summary>
    public const string Unavailable = "unavailable";
}
