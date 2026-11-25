using System.Net;
using Microsoft.AspNetCore.Http;

namespace SharedKernel.AspNetCore.WebAnalytics.Tests;

/// <summary>Builds request contexts that look like a browser loading a page, after the page answered.</summary>
internal static class PageContexts
{
    /// <summary>The visitor's browser.</summary>
    internal const string VisitorAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

    /// <summary>The visitor's address.</summary>
    internal static IPAddress VisitorAddress { get; } = IPAddress.Parse("203.0.113.7");

    /// <summary>The visitor's address as a page view carries it: <see cref="VisitorAddress"/> without its last octet.</summary>
    internal static IPAddress ShortenedVisitorAddress { get; } = IPAddress.Parse("203.0.113.0");

    /// <summary>Creates a GET of <paramref name="path"/> on <c>www.example.com</c> answered with 200 and an HTML page.</summary>
    /// <param name="path">The path, with an optional query string.</param>
    /// <returns>The context.</returns>
    internal static DefaultHttpContext AnsweredPage(string path = "/pt/viagens")
    {
        var context = new DefaultHttpContext();
        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.example.com");
        context.Request.Path = queryStart < 0 ? path : path[..queryStart];
        context.Request.QueryString = queryStart < 0 ? QueryString.Empty : new QueryString(path[queryStart..]);
        context.Request.Headers.UserAgent = VisitorAgent;
        context.Connection.RemoteIpAddress = VisitorAddress;
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context;
    }
}
