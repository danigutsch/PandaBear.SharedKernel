using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>automation</c> command group for argument errors, required environment, workflow outputs, and publish previews.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class RoadmapAutomationCommandTests
{
    /// <summary><c>automation unknown</c> exits with 1 with an unrecognized-command message and does not mention <c>GITHUB_REPOSITORY</c>, even though that variable is unset.</summary>
    [Fact]
    public async Task Unknown_automation_command_is_a_syntax_error_without_ci_environment()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", null);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["automation", "unknown", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unrecognized command or argument 'unknown'.", StringComparison.Ordinal);
        errorText.ShouldNotContain("GITHUB_REPOSITORY", StringComparison.Ordinal);
    }

    /// <summary>Each automation step run with <c>--root .</c> and no <c>GITHUB_REPOSITORY</c> exits with 1, prints nothing to standard output, and reports that roadmap automation requires <c>GITHUB_REPOSITORY</c>.</summary>
    /// <param name="step">The automation subcommand under test.</param>
    [Theory]
    [InlineData("plan-github")]
    [InlineData("validate-plan")]
    [InlineData("apply-plan")]
    [InlineData("seal-result")]
    [InlineData("verify-result")]
    [InlineData("publish-result")]
    public async Task Automation_steps_with_a_relative_root_require_the_repository_variable(string step)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", null);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["automation", step, "--root", "."],
            output,
            error,
            workspace.RootPath,
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: Roadmap automation requires GITHUB_REPOSITORY." + Environment.NewLine);
    }

    /// <summary><c>automation plan-github</c> exits with 0 and writes the plan digest, change flag, branch name, and base SHA to the workflow output file and a summary heading, without copying the untrusted issue title or label into either file.</summary>
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
        var exitCode = await RepoConfigToolRunner.Run(
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
                .ShouldHaveSingleItem(line => line.StartsWith("plan_digest=", StringComparison.Ordinal))["plan_digest=".Length..]);

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

    /// <summary><c>automation publish-result</c> without <c>--force</c> exits with 0 and prints a preview of the branch, draft pull request, and changed file using only read-only GraphQL requests, without writing workflow files or changing the roadmap.</summary>
    [Fact]
    public async Task Publish_command_without_force_previews_the_pull_request_without_changes()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var status = RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json");
        var artifactDigest = new string('c', 64);
        var (plan, seal) = await RoadmapAutomationTestSetup.CreateSealedResult(
            workspace,
            snapshot,
            status,
            artifactDigest,
            TestContext.Current.CancellationToken);
        var roadmapBefore = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var previewHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(previewHandler, snapshot);
        using var previewClient = new HttpClient(previewHandler);
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
        var exitCode = await RepoConfigToolRunner.Run(
            ["automation", "publish-result", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            previewClient,
            RoadmapAutomationTestGit.Create(status),
            TestContext.Current.CancellationToken);
        var roadmapAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        output.ToString().ShouldBe(
            $"preview: roadmap publication would create branch {plan.BranchName} from {RoadmapAutomationTestPlan.BaseSha} in owner/repository and open a draft pull request.{Environment.NewLine}"
            + $"preview: roadmap publication would change roadmap/reconciliation/open-issues-test.json.{Environment.NewLine}");
        previewHandler.Requests.ShouldNotBeEmpty();
        previewHandler.Requests.ShouldAllSatisfy(request =>
        {
            request.PathAndQuery.ShouldBe("/graphql");
            request.Body.ShouldNotBeNull().ShouldNotContain("mutation", StringComparison.Ordinal);
        });
        File.Exists(githubOutputPath).ShouldBeFalse();
        File.Exists(githubSummaryPath).ShouldBeFalse();
        roadmapAfter.ShouldBe(roadmapBefore);
    }

    /// <summary><c>automation publish-result</c> without <c>--force</c> exits with 1 and reports an invalid or tampered result seal when <c>ROADMAP_RESULT_DIGEST</c> does not match, sending no mutation and writing no workflow files.</summary>
    [Fact]
    public async Task Publish_command_without_force_rejects_a_mismatched_result_digest()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var status = RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json");
        var artifactDigest = new string('c', 64);
        var (plan, _) = await RoadmapAutomationTestSetup.CreateSealedResult(
            workspace,
            snapshot,
            status,
            artifactDigest,
            TestContext.Current.CancellationToken);
        using var previewHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(previewHandler, snapshot);
        using var previewClient = new HttpClient(previewHandler);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var githubOutputPath = Path.Combine(workspace.RootPath, "publish-output.txt");
        var githubSummaryPath = Path.Combine(workspace.RootPath, "publish-summary.md");
        using var repository = new EnvironmentVariableScope("GITHUB_REPOSITORY", "owner/repository");
        using var planDigest = new EnvironmentVariableScope("ROADMAP_PLAN_DIGEST", plan.PlanDigest);
        using var artifact = new EnvironmentVariableScope("ROADMAP_ARTIFACT_DIGEST", artifactDigest);
        using var result = new EnvironmentVariableScope("ROADMAP_RESULT_DIGEST", new string('d', 64));
        using var githubOutput = new EnvironmentVariableScope("GITHUB_OUTPUT", githubOutputPath);
        using var githubSummary = new EnvironmentVariableScope("GITHUB_STEP_SUMMARY", githubSummaryPath);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(
            ["automation", "publish-result", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            previewClient,
            RoadmapAutomationTestGit.Create(status),
            TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe(
            "sharedkernel-repo: Roadmap automation result seal is invalid or was tampered with." + Environment.NewLine);
        previewHandler.Requests.ShouldAllSatisfy(request =>
        {
            request.PathAndQuery.ShouldBe("/graphql");
            request.Body.ShouldNotBeNull().ShouldNotContain("mutation", StringComparison.Ordinal);
        });
        File.Exists(githubOutputPath).ShouldBeFalse();
        File.Exists(githubSummaryPath).ShouldBeFalse();
    }

    /// <summary><c>automation publish-result --force</c> exits with 0, prints that the pull request was published, sends a POST to the repository's pulls endpoint, and writes the pull request URL to the workflow output file.</summary>
    [Fact]
    public async Task Publish_command_with_force_writes_a_nonempty_pull_request_url()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var status = RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json");
        var artifactDigest = new string('c', 64);
        var (plan, seal) = await RoadmapAutomationTestSetup.CreateSealedResult(
            workspace,
            snapshot,
            status,
            artifactDigest,
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
        var exitCode = await RepoConfigToolRunner.Run(
            ["automation", "publish-result", "--force", "--root", workspace.RootPath],
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
        output.ToString().ShouldBe("Roadmap automation pull request published." + Environment.NewLine);
        workflowOutputs.ShouldContain(
            $"pull_request_url={RoadmapGitHubPublicationTestOperations.PullRequestUrl}",
            StringComparison.Ordinal);
        publishHandler.Requests.ShouldContain(request =>
            request.Message.Method == HttpMethod.Post
            && string.Equals(request.PathAndQuery, "/repos/owner/repository/pulls", StringComparison.Ordinal));
    }

    /// <summary><c>automation plan-github</c> exits with 1 and asks for <c>GITHUB_TOKEN</c> when only <c>GH_TOKEN</c> is set, without printing the <c>GH_TOKEN</c> value.</summary>
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
        var exitCode = await RepoConfigToolRunner.Run(
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
