using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>
/// Applies response cache policies configured on route groups before endpoint execution.
/// </summary>
public static class ApplicationBuilderHttpCacheExtensions
{
    /// <summary>
    /// Enforces scoped route-group no-store policies when protected responses begin.
    /// </summary>
    /// <remarks>
    /// Register before authentication, authorization, and rate limiting. Endpoint metadata protects matched
    /// requests; configured group prefixes also protect unmatched paths and method rejection responses.
    /// </remarks>
    /// <param name="app">The application pipeline.</param>
    /// <returns>The same builder for middleware composition.</returns>
    public static IApplicationBuilder UseNoStoreResponses(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(next =>
        {
            var dataSource = app.ApplicationServices.GetRequiredService<EndpointDataSource>();
            return context =>
            {
                var protectedEndpoint = context.GetEndpoint()?.Metadata.GetMetadata<NoStoreResponseScope>() is not null;
                if (protectedEndpoint || dataSource.Endpoints.Any(endpoint =>
                        endpoint.Metadata.GetOrderedMetadata<NoStoreResponseScope>().Any(scope =>
                            context.Request.Path.StartsWithSegments(scope.PathPrefix, StringComparison.OrdinalIgnoreCase))))
                {
                    context.Response.OnStarting(static state =>
                    {
                        HttpCacheHeaders.SetNoStore((HttpContext)state);
                        return Task.CompletedTask;
                    }, context);
                }

                return next(context);
            };
        });
    }
}
