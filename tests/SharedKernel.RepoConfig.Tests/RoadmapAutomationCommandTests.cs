using System.Globalization;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class RoadmapAutomationCommandTests
{
    [Fact]
    public async Task Unknown_automation_command_is_a_syntax_error_without_ci_environment()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", null);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["automation", "unknown", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(2);
        errorText.ShouldContain("Unknown automation command: unknown", StringComparison.Ordinal);
        errorText.ShouldNotContain("GITHUB_REPOSITORY", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_command_writes_only_integrity_bound_workflow_outputs()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "untrusted\n::warning::title",
                "CLOSED",
                labels: ["untrusted-label"])
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var githubOutputPath = Path.Combine(workspace.RootPath, "github-output.txt");
        var githubSummaryPath = Path.Combine(workspace.RootPath, "github-summary.md");
        using var runId = new EnvironmentVariableScope("GITHUB_RUN_ID", "42");
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", "owner/repository");
        using var githubOutput = new EnvironmentVariableScope("GITHUB_OUTPUT", githubOutputPath);
        using var githubSummary = new EnvironmentVariableScope("GITHUB_STEP_SUMMARY", githubSummaryPath);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["automation", "plan-github", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            httpClient,
            RoadmapAutomationTestGit.Create(),
            TestContext.Current.CancellationToken);
        var workflowOutputs = await File.ReadAllTextAsync(githubOutputPath, TestContext.Current.CancellationToken);
        var workflowSummary = await File.ReadAllTextAsync(githubSummaryPath, TestContext.Current.CancellationToken);
        var plan = RoadmapAutomationPlan.Load(
            RoadmapAutomationService.GetPlanPath(workspace.RootPath),
            workflowOutputs.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("plan_digest=", StringComparison.Ordinal))["plan_digest=".Length..]);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        workflowOutputs.ShouldContain($"plan_digest={plan.PlanDigest}", StringComparison.Ordinal);
        workflowOutputs.ShouldContain("has_changes=true", StringComparison.Ordinal);
        workflowOutputs.ShouldContain($"branch_name={plan.BranchName}", StringComparison.Ordinal);
        workflowOutputs.ShouldContain($"base_sha={RoadmapAutomationTestPlan.BaseSha}", StringComparison.Ordinal);
        workflowOutputs.ShouldNotContain("untrusted", StringComparison.OrdinalIgnoreCase);
        workflowSummary.ShouldContain("## Roadmap maintenance plan", StringComparison.Ordinal);
        workflowSummary.ShouldNotContain("untrusted", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_command_requires_and_writes_a_nonempty_pull_request_url()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.RefreshReconciliation(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        var status = RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json");
        var artifactDigest = new string('c', 64);
        using var sealHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(sealHandler, snapshot);
        using var sealClient = new HttpClient(sealHandler);
        var sealing = new RoadmapAutomationService(
            workspace.RootPath,
            sealClient,
            RoadmapAutomationTestGit.Create(status));
        var seal = await sealing.SealResult(
            plan.PlanDigest,
            artifactDigest,
            "owner/repository",
            TestContext.Current.CancellationToken);
        using var publishHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(publishHandler, snapshot);
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(publishHandler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(publishHandler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(publishHandler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(publishHandler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(publishHandler);
        using var publishClient = new HttpClient(publishHandler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var githubOutputPath = Path.Combine(workspace.RootPath, "publish-output.txt");
        var githubSummaryPath = Path.Combine(workspace.RootPath, "publish-summary.md");
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", "owner/repository");
        using var planDigest = new EnvironmentVariableScope("ROADMAP_PLAN_DIGEST", plan.PlanDigest);
        using var artifact = new EnvironmentVariableScope("ROADMAP_ARTIFACT_DIGEST", artifactDigest);
        using var result = new EnvironmentVariableScope("ROADMAP_RESULT_DIGEST", seal.ResultDigest);
        using var githubOutput = new EnvironmentVariableScope("GITHUB_OUTPUT", githubOutputPath);
        using var githubSummary = new EnvironmentVariableScope("GITHUB_STEP_SUMMARY", githubSummaryPath);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["automation", "publish-result", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            publishClient,
            RoadmapAutomationTestGit.Create(status),
            TestContext.Current.CancellationToken);
        var workflowOutputs = await File.ReadAllTextAsync(githubOutputPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        workflowOutputs.ShouldContain(
            $"pull_request_url={RoadmapGitHubPublicationTestOperations.PullRequestUrl}",
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Automation_rejects_the_generic_token_fallback()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var runId = new EnvironmentVariableScope("GITHUB_RUN_ID", "42");
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", "owner/repository");
        using var githubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var ghToken = new EnvironmentVariableScope("GH_TOKEN", "ghp_untrusted_fallback");

        // Act
        var exitCode = await RepoConfigToolApplication.Run(
            ["automation", "plan-github", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            httpClient: null,
            RoadmapAutomationTestGit.Create(),
            TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Set GITHUB_TOKEN before running authenticated GitHub roadmap automation.", StringComparison.Ordinal);
        errorText.ShouldNotContain("ghp_untrusted_fallback", StringComparison.Ordinal);
    }
}
