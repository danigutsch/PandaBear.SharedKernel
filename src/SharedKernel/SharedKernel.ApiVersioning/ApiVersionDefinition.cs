namespace SharedKernel.ApiVersioning;

/// <summary>
/// Describes one public API contract version.
/// </summary>
/// <param name="Version">The API contract version.</param>
/// <param name="Status">The lifecycle status for the API contract version.</param>
/// <param name="Deprecation">Optional deprecation metadata.</param>
public sealed record ApiVersionDefinition(
    ApiVersion Version,
    ApiVersionStatus Status = ApiVersionStatus.Active,
    ApiDeprecationPolicy? Deprecation = null)
{
    /// <summary>
    /// Gets the route segment for this version, such as <c>v1</c>.
    /// </summary>
    public string RouteSegment => Version.RouteSegment;

    /// <summary>
    /// Gets the default OpenAPI document name for this version.
    /// </summary>
    public string OpenApiDocumentName => RouteSegment;
}
