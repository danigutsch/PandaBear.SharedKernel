using System.Net;
using Microsoft.Extensions.Http.Resilience;

namespace PandaBear.HttpClients.Tests;

/// <summary>Sends requests that a server answers with 429 through the standard resilience handler, with a customization.</summary>
internal static class TooManyRequestsTestServices
{
    /// <summary>Accepts the requests of the quota-limited routes under <c>/visitor-quota/</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for a quota-limited route.</returns>
    public static bool IsQuotaRoute(HttpRequestMessage request) =>
        request.RequestUri?.AbsolutePath.StartsWith("/visitor-quota/", StringComparison.Ordinal) == true;

    /// <summary>
    /// Sends <paramref name="requests"/> GET requests to <paramref name="path"/>; the server answers <paramref name="failureStatus"/> to the
    /// first <paramref name="failures"/> attempts and 200 afterwards.
    /// </summary>
    /// <param name="configure">The resilience customization.</param>
    /// <param name="path">The relative request path.</param>
    /// <param name="requests">The number of requests.</param>
    /// <param name="failures">The number of failing attempts.</param>
    /// <param name="failureStatus">The status of a failing attempt.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The status of every response and the number of attempts the server received.</returns>
    public static async Task<(HttpStatusCode[] Statuses, int Attempts)> Send(
        Action<HttpStandardResilienceOptions> configure,
        string path,
        int requests,
        int failures,
        HttpStatusCode failureStatus,
        CancellationToken ct)
    {
        using var handler = new TransientAfterCommitHandler(failureStatus, failures);
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler(configure));
        services.AddHttpClient("quota", client => client.BaseAddress = new Uri("https://quota.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("quota");
        List<HttpStatusCode> statuses = [];
        for (var request = 0; request < requests; request++)
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(false);
            statuses.Add(response.StatusCode);
        }

        return ([.. statuses], handler.AttemptCount);
    }
}
