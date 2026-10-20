using System.Net;

namespace SharedKernel.RepoConfig.Tests;

internal static class GitHubSyncTestResponseFactory
{
    public static HttpResponseMessage UntrustedError(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            ReasonPhrase = "ghp_reason_token",
            Content = new StringContent($"{{ \"message\": \"ghp_body_token {new string('x', 4096)}\" }}")
        };
        response.Headers.Add("X-GitHub-Token", "ghp_header_token");
        return response;
    }
}
