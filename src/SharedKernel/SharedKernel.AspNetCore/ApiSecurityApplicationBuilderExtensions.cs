using Microsoft.AspNetCore.Builder;
using PandaBear.OpenApi;

namespace PandaBear.AspNetCore;

/// <summary>
/// Configures API authentication and authorization middleware.
/// </summary>
public static class ApiSecurityApplicationBuilderExtensions
{
    /// <summary>
    /// Adds authentication and authorization middleware when the host serves authenticated API traffic.
    /// </summary>
    /// <param name="application">The application to configure.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication UseApiSecurity(this WebApplication application)
    {
        return UseApiSecurity(application, enableAuthentication: null);
    }

    /// <summary>Adds authentication and authorization middleware unless <paramref name="enableAuthentication" /> or the OpenAPI generation mode disables them.</summary>
    /// <exception cref="ArgumentNullException">When <paramref name="application"/> is <see langword="null"/>.</exception>
    internal static WebApplication UseApiSecurity(WebApplication application, bool? enableAuthentication)
    {
        ArgumentNullException.ThrowIfNull(application);

        var shouldEnableAuthentication = enableAuthentication
            ?? !OpenApiGenerationMode.IsEnabled(application.Environment);

        if (shouldEnableAuthentication)
        {
            application.UseAuthentication();
            application.UseAuthorization();
        }

        return application;
    }
}
