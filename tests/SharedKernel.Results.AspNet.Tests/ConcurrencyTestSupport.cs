using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace PandaBear.Results.AspNet.Tests;

/// <summary>Builds requests and contexts for the concurrency header tests.</summary>
internal static class ConcurrencyTestSupport
{
    /// <summary>A record with a version.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Version">The version.</param>
    internal sealed record Row(string Name, long Version);

    /// <summary>Creates a request that carries the given <c>If-Match</c> value, or none.</summary>
    /// <param name="header">The header value, or <see langword="null"/> to send no header.</param>
    /// <returns>The request.</returns>
    internal static HttpRequest RequestWithIfMatch(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers[HeaderNames.IfMatch] = header;
        }

        return context.Request;
    }

    /// <summary>Creates a context that can execute results and capture their body.</summary>
    /// <returns>The context.</returns>
    internal static DefaultHttpContext NewContext() => new()
    {
        RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        Response =
        {
            Body = new MemoryStream(),
        },
    };
}
