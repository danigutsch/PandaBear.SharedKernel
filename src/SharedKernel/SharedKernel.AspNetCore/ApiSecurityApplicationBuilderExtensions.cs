using Microsoft.AspNetCore.Builder;
using SharedKernel.OpenApi;

namespace SharedKernel.AspNetCore;

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
