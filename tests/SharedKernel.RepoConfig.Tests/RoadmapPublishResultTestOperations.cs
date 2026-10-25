using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

internal static class RoadmapPublishResultTestOperations
{
    private const string ChangedPath = "roadmap/reconciliation/open-issues-test.json";
    private const string ArtifactDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private static readonly IReadOnlyList<GitHubRoadmapReconcileIssue> Snapshot =
    [
        GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
    ];

    /// <summary>
    /// Seals a result in <paramref name="workspace"/> and returns a handler that answers the publication's snapshot reads,
    /// followed by the publication responses with <paramref name="pullRequestUrl"/>.
    /// </summary>
    public static async Task<(TestHttpMessageHandler Handler, string PlanDigest, string ResultDigest)> CreatePublication(
        TemporaryRepoConfigWorkspace workspace,
        Action<TemporaryRepoConfigWorkspace>? configure = null,
        string pullRequestUrl = RoadmapGitHubPublicationTestOperations.PullRequestUrl)
    {
        var (plan, seal) = await RoadmapAutomationTestSetup.CreateSealedResult(
            workspace,
            Snapshot,
            RoadmapGitStatusTestData.Ordinary(ChangedPath),
            ArtifactDigest,
            TestContext.Current.CancellationToken,
            configure);
        var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, Snapshot);
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(handler, pullRequestUrl);
        return (handler, plan.PlanDigest, seal.ResultDigest);
    }

    /// <summary>
    /// Runs <c>automation publish-result</c> with the workflow environment, leaving <paramref name="missingVariable"/>
    /// unset, and returns the workflow output and summary files' content.
    /// </summary>
    public static async Task<(int ExitCode, string Output, string Error, string Outputs, string Summary)> Run(
        TemporaryRepoConfigWorkspace workspace,
        (TestHttpMessageHandler Handler, string PlanDigest, string ResultDigest) publication,
        bool force,
        string? missingVariable = null)
    {
        using var client = new HttpClient(publication.Handler, disposeHandler: false);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var outputPath = Path.Combine(workspace.RootPath, "publish-output.txt");
        var summaryPath = Path.Combine(workspace.RootPath, "publish-summary.md");
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", "owner/repository");
        using var planDigest = new EnvironmentVariableScope("ROADMAP_PLAN_DIGEST", publication.PlanDigest);
        using var artifact = new EnvironmentVariableScope("ROADMAP_ARTIFACT_DIGEST", ArtifactDigest);
        using var result = new EnvironmentVariableScope("ROADMAP_RESULT_DIGEST", publication.ResultDigest);
        using var githubOutput = new EnvironmentVariableScope("GITHUB_OUTPUT", missingVariable == "GITHUB_OUTPUT" ? null : outputPath);
        using var githubSummary = new EnvironmentVariableScope("GITHUB_STEP_SUMMARY", missingVariable == "GITHUB_STEP_SUMMARY" ? null : summaryPath);
        List<string> arguments = ["automation", "publish-result", "--root", workspace.RootPath];
        if (force)
        {
            arguments.Add("--force");
        }

        var exitCode = await RepoConfigToolRunner.Run(
            arguments,
            output,
            error,
            workspace.RootPath,
            client,
            RoadmapAutomationTestGit.Create(RoadmapGitStatusTestData.Ordinary(ChangedPath)),
            TestContext.Current.CancellationToken);
        return (
            exitCode,
            output.ToString(),
            error.ToString(),
            File.Exists(outputPath) ? await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken) : string.Empty,
            File.Exists(summaryPath) ? await File.ReadAllTextAsync(summaryPath, TestContext.Current.CancellationToken) : string.Empty);
    }
}
