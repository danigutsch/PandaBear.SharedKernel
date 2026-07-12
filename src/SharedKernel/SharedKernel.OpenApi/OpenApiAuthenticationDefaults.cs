namespace SharedKernel.OpenApi;

/// <summary>
/// Defines reusable OpenAPI bearer-authentication conventions.
/// </summary>
public static class OpenApiAuthenticationDefaults
{
    /// <summary>
    /// The OpenAPI component name for JWT bearer authentication.
    /// </summary>
    public const string BearerSecuritySchemeName = "Bearer";
}
