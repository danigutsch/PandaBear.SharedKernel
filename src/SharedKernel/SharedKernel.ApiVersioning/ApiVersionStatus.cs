namespace SharedKernel.ApiVersioning;

/// <summary>
/// Describes the lifecycle state of an API contract version.
/// </summary>
public enum ApiVersionStatus
{
    /// <summary>
    /// The version is supported for new and existing consumers.
    /// </summary>
    Active,

    /// <summary>
    /// The version remains available but is planned for removal.
    /// </summary>
    Deprecated,

    /// <summary>
    /// The version is no longer available for request selection.
    /// </summary>
    Retired
}
