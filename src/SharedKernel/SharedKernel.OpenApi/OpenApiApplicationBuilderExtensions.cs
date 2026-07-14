using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.OpenApi;

/// <summary>
/// Maps development-only OpenAPI endpoints.
/// </summary>
public static class OpenApiApplicationBuilderExtensions
{
    /// <summary>
    /// Maps OpenAPI endpoints when the application is running in Development.
    /// </summary>
    /// <param name="application">The application to configure.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication MapConfiguredOpenApi(this WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Environment.IsDevelopment())
        {
            application.MapOpenApi();
        }

        return application;
    }
}
