using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PandaBear.HttpCaching.AspNetCore;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts hosts with default public caching whose endpoints answer in every way the middleware must tell apart.</summary>
internal static class CachingTestHost
{
    /// <summary>The request header that makes the test pipeline sign the user in.</summary>
    internal const string SignedInHeader = "X-Test-Signed-In";

    /// <summary>Starts a host with the middleware.</summary>
    /// <param name="maxAge">The default lifetime.</param>
    /// <returns>The host.</returns>
    internal static Task<PipelineTestHost> Start(TimeSpan maxAge) =>
        PipelineTestHost.Start(app =>
        {
            app.UseDefaultPublicCaching(maxAge);
            app.Use((context, next) =>
            {
                if (context.Request.Headers.ContainsKey(SignedInHeader))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "editor")], "Test"));
                }

                return next(context);
            });
            app.MapMethods("/page", [HttpMethods.Get, HttpMethods.Head, HttpMethods.Post], () => "page");
            app.MapGet("/created", () => TypedResults.Created("/page", "page"));
            app.MapGet("/redirect", () => TypedResults.Redirect("/page"));
            app.MapGet("/own-cache", (HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "no-cache";
                return "page";
            });
            app.MapGet("/header", (HttpContext context, string name, string value) =>
            {
                context.Response.Headers.Append(name, value);
                return "page";
            });
            app.MapGet("/cookie", (HttpContext context) =>
            {
                context.Response.Cookies.Append("session", "1");
                return "page";
            });
        });
}
