using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable security header helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreSecurityHeadersExtensions
{
    private const string ContentSecurityPolicyHeaderName = "Content-Security-Policy";

    private const string XFrameOptionsHeaderName = "X-Frame-Options";

    private const string XContentTypeOptionsHeaderName = "X-Content-Type-Options";

    private const string ReferrerPolicyHeaderName = "Referrer-Policy";

    private const string PermissionsPolicyHeaderName = "Permissions-Policy";

    private const string DefaultPermissionsPolicy = "camera=(), geolocation=(), microphone=(), payment=()";

    /// <summary>
    /// Adds common browser security headers with an application-owned Content Security Policy.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="contentSecurityPolicy">The Content Security Policy value owned by the consuming application.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, string contentSecurityPolicy)
    {
        return app.UseSecurityHeaders(contentSecurityPolicy, DefaultPermissionsPolicy);
    }

    /// <summary>
    /// Adds common browser security headers with application-owned Content Security Policy and Permissions Policy values.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="contentSecurityPolicy">The Content Security Policy value owned by the consuming application.</param>
    /// <param name="permissionsPolicy">The Permissions Policy value owned by the consuming application.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        string contentSecurityPolicy,
        string permissionsPolicy)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSecurityPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionsPolicy);

        return app.Use(async (httpContext, next) =>
        {
            httpContext.Response.OnStarting(static state =>
            {
                var (context, csp, permissions) = ((HttpContext Context, string Csp, string Permissions))state;
                SetSecurityHeaders(context, csp, permissions);
                return Task.CompletedTask;
            }, (httpContext, contentSecurityPolicy, permissionsPolicy));

            await next().ConfigureAwait(false);
        });
    }

    private static void SetSecurityHeaders(HttpContext httpContext, string contentSecurityPolicy, string permissionsPolicy)
    {
        httpContext.Response.Headers[ContentSecurityPolicyHeaderName] = contentSecurityPolicy;
        httpContext.Response.Headers[XFrameOptionsHeaderName] = "DENY";
        httpContext.Response.Headers[ReferrerPolicyHeaderName] = "no-referrer";
        httpContext.Response.Headers[XContentTypeOptionsHeaderName] = "nosniff";
        httpContext.Response.Headers[PermissionsPolicyHeaderName] = permissionsPolicy;
    }
}
