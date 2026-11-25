using System.Net.Http.Headers;

namespace SharedKernel.GitHub;

/// <summary>Creates an <see cref="HttpClient"/> that sends the headers the GitHub REST API expects.</summary>
public static class GitHubApiClient
{
    /// <summary>The REST API version that every request names.</summary>
    public const string ApiVersion = "2026-03-10";

    /// <summary>
    /// Creates a client that owns <paramref name="handler"/> and sends, with every request, <paramref name="userAgent"/>,
    /// <c>Accept: application/vnd.github+json</c>, <c>X-GitHub-Api-Version: 2026-03-10</c>, and, when
    /// <paramref name="token"/> is set, <c>Authorization: Bearer &lt;token&gt;</c>. The client keeps the default
    /// <see cref="HttpClient.Timeout"/>; a caller that times each request itself sets it.
    /// </summary>
    /// <param name="handler">The handler that sends the requests, such as one that refuses redirects. The client disposes it.</param>
    /// <param name="userAgent">The product that names the caller, such as <c>sharedkernel-repo/1.0</c>.</param>
    /// <param name="token">The token from <see cref="GitHubToken.Find"/>, or <see langword="null"/> for anonymous requests.</param>
    /// <returns>The client, which the caller disposes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty or white-space.</exception>
    public static HttpClient Create(HttpMessageHandler handler, ProductInfoHeaderValue userAgent, string? token)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(userAgent);
        if (token is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
        }

        var client = new HttpClient(handler, disposeHandler: true);
        try
        {
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", ApiVersion);
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
