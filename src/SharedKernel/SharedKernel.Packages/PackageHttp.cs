using System.Net;

namespace PandaBear.Packages;

/// <summary>Settings shared by the network sources.</summary>
public static class PackageHttp
{
    /// <summary>The <c>User-Agent</c> that the sources send: a generic client name and the project address, nothing personal.</summary>
    public const string UserAgent = "sharedkernel-packages/1.0 (+https://github.com/danigutsch/PandaBear.SharedKernel)";

    /// <summary>
    /// Creates a handler for the sources: it follows no redirect, keeps no cookies, and limits the time to connect. Give it
    /// to the <see cref="HttpClient"/> that a source uses; the sources also refuse a response that came from another host.
    /// </summary>
    /// <returns>A new handler that the caller disposes through its client.</returns>
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
    };
}
