using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using SharedKernel.GitHub;
using SharedKernel.Repositories.GitHub;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Creates the GitHub connections that the commands use, reading the token from the process environment.</summary>
internal static class GitHubConnections
{
    /// <summary>
    /// Creates a connection that borrows <paramref name="client"/> when it is given, or otherwise authenticates with
    /// <c>GH_TOKEN</c> or <c>GITHUB_TOKEN</c> when an operation first needs it.
    /// </summary>
    /// <param name="client">The client that the tool environment supplies, or <see langword="null"/>.</param>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    public static GitHubConnection FromEnvironment(HttpClient? client, string operation) =>
        Create(
            client,
            GitHubToken.Find(Environment.GetEnvironmentVariable),
            $"Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub {operation}.");

    /// <summary>
    /// Creates a connection that borrows <paramref name="client"/> when it is given, or otherwise authenticates with
    /// <c>GITHUB_TOKEN</c> only, as the GitHub Actions workflow token.
    /// </summary>
    /// <param name="client">The client that the tool environment supplies, or <see langword="null"/>.</param>
    /// <param name="operation">Description of the operation, used in the missing-token message.</param>
    public static GitHubConnection FromGitHubToken(HttpClient? client, string operation) =>
        Create(
            client,
            Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
            $"Set GITHUB_TOKEN before running authenticated GitHub {operation}.");

    /// <summary>Creates an owned GitHub client for reads that can use the public API without a token.</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The returned client owns and disposes the handler.")]
    public static HttpClient CreateReadClient()
    {
        var token = GitHubToken.Find(Environment.GetEnvironmentVariable);
        var handler = CreateHandler();
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

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The returned connection owns and disposes the handler.")]
    private static GitHubConnection Create(HttpClient? client, string? token, string missingTokenMessage)
    {
        if (client is not null)
        {
            return new GitHubConnection(client);
        }

        var handler = CreateHandler();
        try
        {
            return new GitHubConnection(handler, new ProductInfoHeaderValue("sharedkernel-repo", "1.0"), token, missingTokenMessage);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    private static HttpClientHandler CreateHandler() => new() { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
}
