namespace SharedKernel.ApiVersioning;

/// <summary>
/// Describes deprecation metadata for an API contract version.
/// </summary>
/// <param name="DeprecatedOn">The date when the API version became deprecated.</param>
/// <param name="SunsetOn">The planned final availability date.</param>
/// <param name="InformationUrl">An optional URL with migration or lifecycle guidance.</param>
public sealed record ApiDeprecationPolicy(DateOnly? DeprecatedOn = null, DateOnly? SunsetOn = null, Uri? InformationUrl = null);
