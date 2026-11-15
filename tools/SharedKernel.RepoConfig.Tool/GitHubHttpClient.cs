using System.Net.Http.Headers;
using PandaBear.GitHub;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Creates authenticated HTTP clients for the GitHub API.</summary>
internal static class GitHubHttpClient
{
    /// <summary>Creates a client authenticated with <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c>.</summary>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="operation"/> is <see langword="null"/>, empty, or white-space.</exception>
    public static HttpClient Create(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var token = GitHubToken.Find(Environment.GetEnvironmentVariable);
        return Create(token, $"Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub {operation}.");
    }

    /// <summary>Creates a client authenticated with <c>GITHUB_TOKEN</c>.</summary>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="operation"/> is <see langword="null"/>, empty, or white-space.</exception>
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

        var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
        try
        {
            return GitHubApiClient.Create(handler, new ProductInfoHeaderValue("sharedkernel-repo", "1.0"), token);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }
}
