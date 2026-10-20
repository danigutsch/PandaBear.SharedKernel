using System.Globalization;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RepoConfigToolCoverageTests
{
    [Fact]
    public async Task Program_help_returns_success()
    {
        // Arrange
        string[] args = ["--help"];

        // Act
        var exitCode = await Program.Main(args);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_command_returns_usage_error()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["unknown"], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(2);
        errorText.ShouldContain("Unknown command: unknown", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_reports_when_structure_already_exists()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var firstOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var firstError = new StringWriter(CultureInfo.InvariantCulture);
        using var secondOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var secondError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], firstOutput, firstError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], secondOutput, secondError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = secondOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("Repository roadmap structure is already initialized.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Diff_reports_valid_and_invalid_structure()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var validOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var validError = new StringWriter(CultureInfo.InvariantCulture);
        using var invalidOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var invalidError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);

        // Act
        var validExitCode = await RepoConfigToolApplication.Run(["diff", "--root", workspace.RootPath], validOutput, validError, workspace.RootPath, TestContext.Current.CancellationToken);
        workspace.DeleteFile("roadmap/config.json");
        var invalidExitCode = await RepoConfigToolApplication.Run(["diff", "--root", workspace.RootPath], invalidOutput, invalidError, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        validExitCode.ShouldBe(0);
        validOutput.ToString().ShouldContain("Repository config has no drift.", StringComparison.Ordinal);
        invalidExitCode.ShouldBe(1);
        invalidError.ToString().ShouldContain("Repository config drift:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set_reports_usage_and_unsupported_key_errors()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var missingOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var missingError = new StringWriter(CultureInfo.InvariantCulture);
        using var extraOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var extraError = new StringWriter(CultureInfo.InvariantCulture);
        using var unsupportedOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var unsupportedError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);

        // Act
        var missingExitCode = await RepoConfigToolApplication.Run(["set", "--root", workspace.RootPath], missingOutput, missingError, workspace.RootPath, TestContext.Current.CancellationToken);
        var extraExitCode = await RepoConfigToolApplication.Run(["set", "github.repository", "owner/repo", "extra", "--root", workspace.RootPath], extraOutput, extraError, workspace.RootPath, TestContext.Current.CancellationToken);
        var unsupportedExitCode = await RepoConfigToolApplication.Run(["set", "unknown", "value", "--root", workspace.RootPath], unsupportedOutput, unsupportedError, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        missingExitCode.ShouldBe(2);
        missingError.ToString().ShouldContain("Missing set key and value.", StringComparison.Ordinal);
        extraExitCode.ShouldBe(2);
        extraError.ToString().ShouldContain("Unknown argument: extra", StringComparison.Ordinal);
        unsupportedExitCode.ShouldBe(1);
        unsupportedError.ToString().ShouldContain("sharedkernel-repo: Unsupported config key: unknown", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_project_queries_cover_priority_taxonomy_and_blocker_views()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson);
        workspace.WriteFile("roadmap/items/RM-003-enabler.json", RoadmapTestContent.UnblockedEnablerJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\", \"RM-003\"] }");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var nextIssueOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var nextIssueError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var nextPriority = await RepoConfigToolApplication.Run(["get", "next-priority", "--limit", "2", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var nextBlockers = await RepoConfigToolApplication.Run(["get", "next-blockers", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var nextEnablers = await RepoConfigToolApplication.Run(["get", "next-enablers", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var lowHangingFruit = await RepoConfigToolApplication.Run(["get", "low-hanging-fruit", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var pareto = await RepoConfigToolApplication.Run(["get", "pareto", "--limit", "1", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var blockingOverview = await RepoConfigToolApplication.Run(["get", "blocking-overview", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var tags = await RepoConfigToolApplication.Run(["get", "tags", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var labels = await RepoConfigToolApplication.Run(["get", "labels", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var byTag = await RepoConfigToolApplication.Run(["get", "by-tag", "enabler", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var byLabel = await RepoConfigToolApplication.Run(["get", "by-label", "area: tooling", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var nextIssue = await RepoConfigToolApplication.Run(["get", "next-unblocked", "--type", "issue", "--root", workspace.RootPath], nextIssueOutput, nextIssueError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = output.ToString();
        var nextIssueOutputText = nextIssueOutput.ToString();

        // Assert
        nextPriority.ShouldBe(0);
        nextBlockers.ShouldBe(0);
        nextEnablers.ShouldBe(0);
        lowHangingFruit.ShouldBe(0);
        pareto.ShouldBe(0);
        blockingOverview.ShouldBe(0);
        tags.ShouldBe(0);
        labels.ShouldBe(0);
        byTag.ShouldBe(0);
        byLabel.ShouldBe(0);
        nextIssue.ShouldBe(0);
        outputText.ShouldContain("RM-001 | epic", StringComparison.Ordinal);
        outputText.ShouldContain("RM-003 | enabler", StringComparison.Ordinal);
        outputText.ShouldContain("RM-002 blocked by RM-001", StringComparison.Ordinal);
        outputText.ShouldContain("enabler | 1", StringComparison.Ordinal);
        outputText.ShouldContain("area: tooling | 3", StringComparison.Ordinal);
        nextIssueOutputText.ShouldContain("No matching roadmap items.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_option_validation_reports_usage_before_loading_config()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var missingType = await RepoConfigToolApplication.Run(["get", "next-priority", "--type", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var invalidLimit = await RepoConfigToolApplication.Run(["get", "next-priority", "--limit", "none", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var unknownOption = await RepoConfigToolApplication.Run(["get", "next-priority", "--unknown", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        missingType.ShouldBe(2);
        invalidLimit.ShouldBe(2);
        unknownOption.ShouldBe(2);
        errorText.ShouldContain("Missing required value for --type.", StringComparison.Ordinal);
        errorText.ShouldContain("Missing or invalid required value for --limit.", StringComparison.Ordinal);
        errorText.ShouldContain("Unknown get option: --unknown", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_reports_usage_errors_and_empty_mappings()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);

        // Act
        var missingTarget = await RepoConfigToolApplication.Run(["sync", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var conflictingFlags = await RepoConfigToolApplication.Run(["sync", "github", "--dry-run", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var unknownArgument = await RepoConfigToolApplication.Run(["sync", "github", "--unknown", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var emptyMappings = await RepoConfigToolApplication.Run(["sync", "github", "--dry-run", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();
        var outputText = output.ToString();

        // Assert
        missingTarget.ShouldBe(2);
        conflictingFlags.ShouldBe(2);
        unknownArgument.ShouldBe(2);
        emptyMappings.ShouldBe(0);
        errorText.ShouldContain("Missing sync target: github.", StringComparison.Ordinal);
        errorText.ShouldContain("Use either --dry-run or --apply, not both.", StringComparison.Ordinal);
        errorText.ShouldContain("Unknown sync argument.", StringComparison.Ordinal);
        outputText.ShouldContain("No roadmap items have GitHub issue mappings or explicit creation requests.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_propagates_http_request_errors_for_application_boundary()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("network unavailable"));
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<HttpRequestException>();

        // Assert
        exception.Message.ShouldContain("network unavailable", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_github_apply_reports_safe_diagnostic_for_http_request_errors()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("ghp_transport_token"));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["sync", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = output.ToString();
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        outputText.ShouldBe(string.Empty);
        errorText.ShouldBe("sharedkernel-repo: GitHub sync request failed." + Environment.NewLine);
        errorText.ShouldNotContain("ghp_transport_token", StringComparison.Ordinal);
    }

    [Fact]
    public void Try_parse_json_file_reports_read_failures_as_issues()
    {
        // Arrange
        List<RepoConfigIssue> issues = [];

        // Act
        var document = RepoConfigVerifier.TryParseJsonFile("/repository", "/repository/roadmap/config.json", issues, _ => throw new IOException("simulated read failure"));

        // Assert
        document.ShouldBeNull();
        issues.Count.ShouldBe(1);
        issues[0].Path.ShouldBe("roadmap/config.json");
        issues[0].Message.ShouldBe("Unable to read JSON: simulated read failure");
    }

    [Fact]
    public async Task Sync_github_apply_reports_safe_diagnostic_for_timeouts()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        using var handler = TestHttpMessageHandler.FromException(GitHubRequestTimeout.Create("sync"));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["sync", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = output.ToString();
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        outputText.ShouldBe(string.Empty);
        errorText.ShouldBe("sharedkernel-repo: GitHub sync timed out after 30 seconds." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_reports_config_and_item_policy_violations()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        workspace.WriteFile("roadmap/config.json", RoadmapTestContent.InvalidConfigJson);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", RoadmapTestContent.InvalidItemJson);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["verify", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContainInOrder(
            "sourceOfTruth must be repository.",
            "allowed.statuses must contain at least one status.",
            "project.ordering must be order.",
            "integrations.github.enabled must be a Boolean.",
            "scoring.model must be RICE.");
        errorText.ShouldContain("id must start with RM-.", StringComparison.Ordinal);
        errorText.ShouldContain("Unknown roadmap type: mystery.", StringComparison.Ordinal);
        errorText.ShouldContain("order must be 1 or greater.", StringComparison.Ordinal);
        errorText.ShouldContain("blockedBy must be an array.", StringComparison.Ordinal);
    }
}
