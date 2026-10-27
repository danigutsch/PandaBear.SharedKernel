using System.Net;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates GitHub responses for the sync tests.</summary>
internal static class GitHubSyncTestResponseFactory
{
    /// <summary>Returns the JSON metadata of an issue with the given number and labels.</summary>
    public static string IssueMetadata(int issueNumber = 997, string labels = "[]") =>
        $$"""{"number":{{issueNumber}},"node_id":"I_{{issueNumber}}","html_url":"https://github.com/owner/repository/issues/{{issueNumber}}","labels":{{labels}}}""";

    /// <summary>Creates an error response whose reason, body, and headers contain token-like text that the tool must not echo.</summary>
    public static HttpResponseMessage UntrustedError(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            ReasonPhrase = "ghp_reason_token",
            Content = new StringContent($"{{ \"message\": \"ghp_body_token {new string('x', 4096)}\" }}")
        };
        response.Headers.Add("X-GitHub-Token", "ghp_header_token");
        if (statusCode == HttpStatusCode.Forbidden)
        {
            response.Headers.Add("X-RateLimit-Remaining", "0");
        }
        return response;
    }
}
