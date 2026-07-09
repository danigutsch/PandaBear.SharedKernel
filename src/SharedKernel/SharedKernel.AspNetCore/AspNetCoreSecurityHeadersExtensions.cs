using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable security header helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreSecurityHeadersExtensions
{
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
            SetSecurityHeaders(httpContext, contentSecurityPolicy, permissionsPolicy);
            await next(httpContext).ConfigureAwait(false);
        });
    }

    private static void SetSecurityHeaders(HttpContext httpContext, string contentSecurityPolicy, string permissionsPolicy)
    {
        httpContext.Response.Headers.ContentSecurityPolicy = contentSecurityPolicy;
        httpContext.Response.Headers.XFrameOptions = "DENY";
        httpContext.Response.Headers[ReferrerPolicyHeaderName] = "no-referrer";
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers[PermissionsPolicyHeaderName] = permissionsPolicy;
    }
}
