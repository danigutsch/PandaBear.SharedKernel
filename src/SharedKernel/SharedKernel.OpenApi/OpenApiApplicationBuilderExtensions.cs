using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace PandaBear.OpenApi;

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="application"/> is <see langword="null"/>.</exception>
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
