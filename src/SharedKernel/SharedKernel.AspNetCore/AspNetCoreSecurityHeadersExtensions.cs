using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

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
    /// The token that the middleware replaces with the request's <see cref="ICspNonce" /> in the Content Security Policy, written inside a
    /// CSP3 nonce source such as <c>style-src 'self' 'nonce-{nonce}'</c>. A policy that contains it needs
    /// <see cref="CspNonceServiceCollectionExtensions.AddCspNonce" />.
    /// </summary>
    public const string NoncePlaceholder = "{nonce}";

    /// <summary>
    /// Adds common browser security headers with an application-owned Content Security Policy.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="contentSecurityPolicy">
    /// The Content Security Policy value owned by the consuming application. Every <see cref="NoncePlaceholder" /> in it is replaced with the
    /// request's <see cref="ICspNonce" />, and a <c>Cache-Control</c> that allows shared caching (<c>public</c> or <c>s-maxage</c>) on that response
    /// becomes <c>private</c>, keeping <c>max-age</c>.
    /// </param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the policy contains <see cref="NoncePlaceholder" /> and <see cref="ICspNonce" /> is not registered.</exception>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, string contentSecurityPolicy)
    {
        return app.UseSecurityHeaders(contentSecurityPolicy, DefaultPermissionsPolicy);
    }

    /// <summary>
    /// Adds common browser security headers with application-owned Content Security Policy and Permissions Policy values.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="contentSecurityPolicy">
    /// The Content Security Policy value owned by the consuming application. Every <see cref="NoncePlaceholder" /> in it is replaced with the
    /// request's <see cref="ICspNonce" />, and a <c>Cache-Control</c> that allows shared caching (<c>public</c> or <c>s-maxage</c>) on that response
    /// becomes <c>private</c>, keeping <c>max-age</c>.
    /// </param>
    /// <param name="permissionsPolicy">The Permissions Policy value owned by the consuming application.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="contentSecurityPolicy"/> or <paramref name="permissionsPolicy"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/>, <paramref name="contentSecurityPolicy"/>, or <paramref name="permissionsPolicy"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the policy contains <see cref="NoncePlaceholder" /> and <see cref="ICspNonce" /> is not registered.</exception>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        string contentSecurityPolicy,
        string permissionsPolicy)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSecurityPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionsPolicy);
        var usesNonce = contentSecurityPolicy.Contains(NoncePlaceholder, StringComparison.Ordinal);
        if (usesNonce && app.ApplicationServices.GetService<IServiceProviderIsService>()?.IsService(typeof(ICspNonce)) != true)
        {
            throw new InvalidOperationException(
                $"The Content Security Policy contains {NoncePlaceholder}, but {nameof(ICspNonce)} is not registered. Call services.{nameof(CspNonceServiceCollectionExtensions.AddCspNonce)}().");
        }

        return app.Use(async (httpContext, next) =>
        {
            httpContext.Response.OnStarting(static state =>
            {
                var (context, csp, permissions, nonce) = ((HttpContext Context, string Csp, string Permissions, bool Nonce))state;
                if (nonce)
                {
                    csp = csp.Replace(NoncePlaceholder, context.RequestServices.GetRequiredService<ICspNonce>().Value, StringComparison.Ordinal);
                    KeepOutOfSharedCaches(context.Response);
                }

                SetSecurityHeaders(context, csp, permissions);
                return Task.CompletedTask;
            }, (httpContext, contentSecurityPolicy, permissionsPolicy, usesNonce));

            await next().ConfigureAwait(false);
        });
    }

    private static void KeepOutOfSharedCaches(HttpResponse response)
    {
        var current = response.Headers.CacheControl;
        if (current.Count == 0)
        {
            return;
        }

        if (!CacheControlHeaderValue.TryParse(current.ToString(), out var cacheControl))
        {
            response.Headers.CacheControl = "private, no-cache";
            return;
        }

        if (cacheControl.Public || cacheControl.SharedMaxAge.HasValue)
        {
            cacheControl.Public = false;
            cacheControl.SharedMaxAge = null;
            cacheControl.Private = true;
            response.Headers.CacheControl = cacheControl.ToString();
        }
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
