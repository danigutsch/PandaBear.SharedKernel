using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using PandaBear.GitHub;
using PandaBear.Repositories.GitHub;

namespace PandaBear.RepoConfig.Tool;

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

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The returned connection owns and disposes the handler.")]
    private static GitHubConnection Create(HttpClient? client, string? token, string missingTokenMessage)
    {
        if (client is not null)
        {
            return new GitHubConnection(client);
        }

        var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
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
}
