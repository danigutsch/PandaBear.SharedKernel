using System.Net.Http.Headers;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Creates authenticated HTTP clients for the GitHub API.</summary>
internal static class GitHubHttpClient
{
    /// <summary>Creates a client authenticated with <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c>.</summary>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    public static HttpClient Create(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return Create(token, $"Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub {operation}.");
    }

    /// <summary>Creates a client authenticated with <c>GITHUB_TOKEN</c>.</summary>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    public static HttpClient CreateFromGitHubToken(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return Create(token, $"Set GITHUB_TOKEN before running authenticated GitHub {operation}.");
    }

    private static HttpClient Create(string? token, string missingTokenMessage)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(missingTokenMessage);
        }

        HttpClientHandler? handler = null;
        HttpClient? httpClient = null;
        try
        {
            handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
            httpClient = new HttpClient(handler, disposeHandler: true);
            handler = null;
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("sharedkernel-repo", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var result = httpClient;
            httpClient = null;
            return result;
        }
        finally
        {
            handler?.Dispose();
            httpClient?.Dispose();
        }
    }
}
