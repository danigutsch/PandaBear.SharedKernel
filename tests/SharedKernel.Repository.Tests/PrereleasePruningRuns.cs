using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Runs <c>prune-prereleases</c> through the real command tree against a <see cref="FakeGitHubReleasesApi"/>.</summary>
internal static class PrereleasePruningRuns
{
    /// <summary>The token that runs read from <c>GITHUB_TOKEN</c> unless a test passes another.</summary>
    public const string Token = "ghs_test-token-value";

    /// <summary>Uses <paramref name="api"/> and reads <paramref name="token"/> as <c>GITHUB_TOKEN</c>, with no <c>GH_TOKEN</c>.</summary>
    public static PrereleasePruningServices Services(FakeGitHubReleasesApi api, string? token) =>
        new(() => api, name => name == "GITHUB_TOKEN" ? token : null);

    /// <summary>Runs <c>prune-prereleases --repository</c> for the fake's repository with <paramref name="options"/>.</summary>
    public static Task<RepositoryToolResult> Run(FakeGitHubReleasesApi api, string[] options, string? token = Token) =>
        RepositoryToolInvocation.RunPruning(
            ["prune-prereleases", "--repository", FakeGitHubReleasesApi.Repository, .. options],
            Services(api, token),
            TestContext.Current.CancellationToken);
}
