using System.Net.Http.Headers;

namespace SharedKernel.RepoConfig.Tool;

internal static class GitHubHttpClient
{
    public static HttpClient Create(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException($"Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub {operation}.");
        }

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("sharedkernel-repo", "1.0"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return httpClient;
    }
}
