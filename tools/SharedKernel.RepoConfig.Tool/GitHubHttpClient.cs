using System.Net.Http.Headers;

namespace SharedKernel.RepoConfig.Tool;

internal static class GitHubHttpClient
{
    public static HttpClient Create(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return Create(token, $"Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub {operation}.");
    }

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

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("sharedkernel-repo", "1.0"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return httpClient;
    }
}
