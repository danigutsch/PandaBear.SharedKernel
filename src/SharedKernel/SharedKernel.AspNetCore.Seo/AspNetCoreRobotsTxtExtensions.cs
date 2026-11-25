using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.AspNetCore.Seo;

/// <summary>
/// Provides reusable robots.txt endpoint helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreRobotsTxtExtensions
{
    private const string RobotsTxtPath = "/robots.txt";

    private const string RobotsTxtContentType = "text/plain; charset=utf-8";

    /// <summary>
    /// Maps an anonymous root robots.txt endpoint with application-owned crawler policy text for <c>GET</c> and <c>HEAD</c>. Both answer
    /// with <c>text/plain; charset=utf-8</c> and the <c>Content-Length</c> of the text; only <c>GET</c> writes the body.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="robotsTxt">The robots.txt content owned by the consuming application.</param>
    /// <returns>The mapped endpoint convention builder, explicitly marked anonymous.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="robotsTxt"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> or <paramref name="robotsTxt"/> is <see langword="null"/>.</exception>
    public static IEndpointConventionBuilder MapRobotsTxt(this IEndpointRouteBuilder app, string robotsTxt)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(robotsTxt);

        var body = Encoding.UTF8.GetBytes(robotsTxt);
        return app.MapMethods(RobotsTxtPath, [HttpMethods.Get, HttpMethods.Head], context => WriteRobotsTxt(context, body))
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    private static Task WriteRobotsTxt(HttpContext context, byte[] body)
    {
        context.Response.ContentType = RobotsTxtContentType;
        context.Response.ContentLength = body.Length;
        return HttpMethods.IsHead(context.Request.Method)
            ? Task.CompletedTask
            : context.Response.Body.WriteAsync(body, context.RequestAborted).AsTask();
    }
}
