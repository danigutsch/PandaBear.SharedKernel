using System.Net.Http.Headers;

namespace PandaBear.Forgejo;

/// <summary>Creates an <see cref="HttpClient"/> for the REST API of a Forgejo instance.</summary>
public static class ForgejoApiClient
{
    /// <summary>
    /// Creates a client that owns <paramref name="handler"/>, whose base address is the <c>/api/v1/</c> endpoint of
    /// <paramref name="server"/>, and that sends, with every request, <paramref name="userAgent"/>,
    /// <c>Accept: application/json</c>, and, when <paramref name="token"/> is set, <c>Authorization: token &lt;token&gt;</c>, the
    /// form that <see href="https://forgejo.org/docs/latest/user/api-usage/">the Forgejo API</see> documents. The client keeps
    /// the default <see cref="HttpClient.Timeout"/>; a caller that times each request itself sets it.
    /// </summary>
    /// <param name="handler">The handler that sends the requests, such as one that refuses redirects. The client disposes it.</param>
    /// <param name="server">The HTTPS address of the instance, such as <c>https://codeberg.org</c>, without a query or fragment.</param>
    /// <param name="userAgent">The product that names the caller, such as <c>sharedkernel-ci/1.0</c>.</param>
    /// <param name="token">The token from <see cref="ForgejoToken.Find"/>, or <see langword="null"/> for anonymous requests.</param>
    /// <returns>The client, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/>, <paramref name="server"/>, or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> is not an absolute HTTPS address without a query or fragment, or <paramref name="token"/> is empty or white-space.</exception>
    public static HttpClient Create(HttpMessageHandler handler, Uri server, ProductInfoHeaderValue userAgent, string? token)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(userAgent);
        var apiBase = ApiBase(server);
        if (token is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
        }

        var client = new HttpClient(handler, disposeHandler: true) { BaseAddress = apiBase };
        try
        {
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>Returns the <c>/api/v1/</c> endpoint of <paramref name="server"/>.</summary>
    /// <param name="server">The HTTPS address of the instance; a path, such as that of an instance behind a sub-path, is kept.</param>
    /// <returns>The endpoint, ending with <c>/</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="server"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="server"/> is not an absolute HTTPS address without user information, a query, or a fragment.</exception>
    public static Uri ApiBase(Uri server)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (!server.IsAbsoluteUri
            || server.Scheme != Uri.UriSchemeHttps
            || server.UserInfo.Length > 0
            || server.Query.Length > 0
            || server.Fragment.Length > 0)
        {
            throw new ArgumentException("The Forgejo server must be an absolute HTTPS address without user information, a query, or a fragment.", nameof(server));
        }

        return new Uri(server.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/api/v1/");
    }
}
