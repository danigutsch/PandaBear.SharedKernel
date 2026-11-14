using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts hosts with both the security headers and the default public caching, registered in either order.</summary>
internal static class NonceCachingTestHost
{
    /// <summary>The default public lifetime.</summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Starts a host whose <c>/page</c> sets no cache header and whose <c>/own-public</c> sets
    /// <c>public, max-age=300, s-maxage=600</c> itself.
    /// </summary>
    /// <param name="policy">The Content Security Policy.</param>
    /// <param name="securityHeadersFirst">Whether the security headers are registered before the default public caching.</param>
    /// <returns>The started host.</returns>
    internal static Task<PipelineTestHost> Start(string policy, bool securityHeadersFirst) => PipelineTestHost.Start(
        services => services.AddCspNonce(),
        app =>
        {
            if (securityHeadersFirst)
            {
                app.UseSecurityHeaders(policy);
                app.UseDefaultPublicCaching(MaxAge);
            }
            else
            {
                app.UseDefaultPublicCaching(MaxAge);
                app.UseSecurityHeaders(policy);
            }

            app.MapGet("/page", () => "page");
            app.MapGet("/own-public", (HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "public, max-age=300, s-maxage=600";
                return "page";
            });
        });
}
