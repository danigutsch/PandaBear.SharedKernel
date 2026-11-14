using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>
/// Applies response cache policies configured on route groups before endpoint execution.
/// </summary>
public static class ApplicationBuilderHttpCacheExtensions
{
    /// <summary>
    /// Enforces scoped route-group no-store policies when protected responses begin.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Makes anonymous successful pages publicly cacheable for a short time: when a response starts, it gets
    /// <c>Cache-Control: public, max-age=N</c> only if the request is GET or HEAD, the status is 200, the response has no
    /// <c>Cache-Control</c> of its own and sets no cookie, its content security policy holds no per-request <c>'nonce-…'</c> source, the
    /// request has no <c>Authorization</c> header, and the user is not authenticated. Every other response is left untouched.
    /// </summary>
    /// <remarks>
    /// The user is checked when the response starts, so authentication that runs later in the pipeline is seen. Endpoints that want another
    /// lifetime, such as immutable files, set their own header and keep it. Register it early, before the endpoints.
    /// </remarks>
    /// <param name="app">The application pipeline.</param>
    /// <param name="maxAge">The freshness lifetime, at least one second; it is written in whole seconds.</param>
    /// <returns>The same builder for middleware composition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxAge"/> is less than one second.</exception>
    public static IApplicationBuilder UseDefaultPublicCaching(this IApplicationBuilder app, TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAge, TimeSpan.FromSeconds(1));

        var headerValue = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromSeconds(Math.Floor(maxAge.TotalSeconds)) }.ToString();
        return app.Use((context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.OnStarting(
                    static state =>
                    {
                        var (httpContext, value) = ((HttpContext, string))state;
                        if (IsAnonymousSharedResponse(httpContext))
                        {
                            httpContext.Response.Headers[HeaderNames.CacheControl] = value;
                        }

                        return Task.CompletedTask;
                    },
                    (context, headerValue));
            }

            return next(context);
        });
    }

    private static bool IsAnonymousSharedResponse(HttpContext context) =>
        context.Response.StatusCode == StatusCodes.Status200OK
        && !context.Response.Headers.ContainsKey(HeaderNames.CacheControl)
        && !context.Response.Headers.ContainsKey(HeaderNames.SetCookie)
        && !context.Request.Headers.ContainsKey(HeaderNames.Authorization)
        && context.User.Identity?.IsAuthenticated != true
        && !HoldsNonce(context.Response.Headers[HeaderNames.ContentSecurityPolicy])
        && !HoldsNonce(context.Response.Headers[HeaderNames.ContentSecurityPolicyReportOnly]);

    private static bool HoldsNonce(StringValues policies)
    {
        foreach (var policy in policies)
        {
            if (policy?.Contains("'nonce-", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }
}
