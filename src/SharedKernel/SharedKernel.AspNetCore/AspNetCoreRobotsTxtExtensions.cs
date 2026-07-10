using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable robots.txt endpoint helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreRobotsTxtExtensions
{
    private const string RobotsTxtPath = "/robots.txt";

    private const string RobotsTxtContentType = "text/plain; charset=utf-8";

    /// <summary>
    /// Maps a root robots.txt endpoint with application-owned crawler policy text.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="robotsTxt">The robots.txt content owned by the consuming application.</param>
    /// <returns>The mapped endpoint convention builder.</returns>
    public static IEndpointConventionBuilder MapRobotsTxt(this IEndpointRouteBuilder app, string robotsTxt)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(robotsTxt);

        return app.MapGet(RobotsTxtPath, context => WriteRobotsTxt(context, robotsTxt))
            .ExcludeFromDescription();
    }

    private static Task WriteRobotsTxt(HttpContext context, string robotsTxt)
    {
        context.Response.ContentType = RobotsTxtContentType;
        return context.Response.WriteAsync(robotsTxt, Encoding.UTF8, context.RequestAborted);
    }
}
