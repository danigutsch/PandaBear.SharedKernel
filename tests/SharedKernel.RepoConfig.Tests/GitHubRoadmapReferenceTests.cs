using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests how roadmap import and reconcile treat references to issues in other repositories.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapReferenceTests
{
    /// <summary>Verifies that an external issue mapping neither requests a local issue with the same number nor writes to GitHub.</summary>
    [Fact]
    public async Task External_issue_mapping_does_not_request_a_same_number_local_issue_or_remote_write()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var content = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("\"issue\": 100", "\"issue\": 100, \"repository\": \"other/library\", \"nodeId\": \"I_external\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-018-github-100.json", content);
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);

        // Act
        var required = project.GetGitHubIssueNumbers();
        var result = await new GitHubRoadmapSyncer(project, client).Apply(TestContext.Current.CancellationToken);

        // Assert
        required.ShouldBeEmpty();
        result.Messages.ShouldContain("No roadmap items have GitHub issue mappings or explicit creation requests.");
        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(content);
    }

    /// <summary>Verifies that the same issue number in two repositories is not treated as a duplicate identity.</summary>
    [Fact]
    public async Task Same_issue_number_in_different_repositories_is_not_a_duplicate_identity()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var content = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("RM-018", "RM-019", StringComparison.Ordinal)
            .Replace("\"order\": 11", "\"order\": 12", StringComparison.Ordinal)
            .Replace("\"issue\": 100", "\"issue\": 100, \"repository\": \"other/library\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-019-github-100.json", content);
        var order = workspace.ReadFile("roadmap/order.json").Replace("\"RM-018\"", "\"RM-018\", \"RM-019\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/order.json", order);

        // Act
        var issues = RepoConfigVerifier.Verify(workspace.RootPath);

        // Assert
        issues.ShouldBeEmpty();
    }
    /// <summary>Verifies that import keeps the identity of an external parent and the reviewed priority.</summary>
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    public async Task Import_preserves_external_parent_identity_and_reviewed_priority(bool existingChild, bool snapshotHasParent, bool externalParent)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var external = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("\"issue\": 100", "\"issue\": 18, \"repository\": \"other/library\", \"previousLocations\": [{\"repository\":\"owner/repository\",\"issue\":100}]", StringComparison.Ordinal);
        if (!externalParent)
        {
            external = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        }

        if (existingChild)
        {
            external = external.Replace("\"blocks\": []", "\"blocks\": [\"RM-019\"]", StringComparison.Ordinal);
            var child = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
                .Replace("RM-018", "RM-019", StringComparison.Ordinal)
                .Replace("\"order\": 11", "\"order\": 12", StringComparison.Ordinal)
                .Replace("\"issue\": 100", "\"issue\": 101", StringComparison.Ordinal)
                .Replace("\"blockedBy\": []", "\"blockedBy\": [\"RM-018\"]", StringComparison.Ordinal)
                .Replace("\"id\": \"RM-019\"", "\"id\": \"RM-019\", \"parent\": \"RM-018\"", StringComparison.Ordinal);
            workspace.WriteFile("roadmap/items/RM-019-github-101.json", child);
            workspace.WriteFile("roadmap/order.json", workspace.ReadFile("roadmap/order.json").Replace("\"RM-018\"", "\"RM-018\", \"RM-019\"", StringComparison.Ordinal));
        }

        workspace.WriteFile("roadmap/items/RM-018-github-100.json", external);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "Local child", "OPEN", parent: snapshotHasParent ? GitHubRoadmapSnapshotTestOperations.Relation(18, "OPEN", "other/library") : null)
        ];
        const string manifestPath = "roadmap/reconciliation/open-issues-test.json";
        var manifest = JsonNode.Parse(workspace.ReadFile(manifestPath))!.AsObject();
        manifest["needsHuman"] = new JsonArray(101);
        manifest["integrity"]!["expectedIssueCount"] = 1;
        manifest["integrity"]!["expectedNeedsHumanCount"] = 1;
        GitHubRoadmapSnapshotTestOperations.Configure(workspace, manifest.ToJsonString(), snapshot);
        using var handler = new TestHttpMessageHandler();
        if (externalParent)
        {
            GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        }
        else
        {
            handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(snapshot));
            handler.EnqueueJson(System.Net.HttpStatusCode.OK, """{"data":{"repository":{"issueOrPullRequest":null}},"errors":[{"type":"NOT_FOUND","path":["repository","issueOrPullRequest"]}]}""");
            handler.EnqueueJson(System.Net.HttpStatusCode.NotFound, "{}");
            handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
        }

        using var client = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldBe(string.Empty);
        exitCode.ShouldBe(0);
        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(external);
        var imported = JsonNode.Parse(workspace.ReadFile("roadmap/items/RM-019-github-101.json"))!;
        var parent = imported["parent"]!.GetValue<string>();
        parent.ShouldBe("RM-018");
        if (existingChild)
        {
            var blocker = imported["blockedBy"]![0]!.GetValue<string>();
            blocker.ShouldBe("RM-018");
        }

        RepoConfigVerifier.Verify(workspace.RootPath).ShouldBeEmpty();
    }

    /// <summary>Verifies that reconcile resolves transferred issues, keeps their history, and does not write to GitHub.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_resolves_transfers_without_losing_history_or_mutating_github(bool preview)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        const string itemPath = "roadmap/items/RM-018-github-100.json";
        var original = workspace.ReadFile(itemPath);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapReferenceTestOperations.EnqueueTransfer(handler);
        using var client = new HttpClient(handler);
        string[] arguments = preview ? ["github", "reconcile", "--root", workspace.RootPath] : ["github", "reconcile", "--force", "--root", workspace.RootPath];

        // Act
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldBe(string.Empty);
        exitCode.ShouldBe(0);
        if (preview)
        {
            workspace.ReadFile(itemPath).ShouldBe(original);
        }
        else
        {
            var current = JsonNode.Parse(workspace.ReadFile(itemPath))!;
            var github = current["integrations"]!["github"]!;
            var number = github["issue"]!.GetValue<int>();
            number.ShouldBe(18);
            var repository = github["repository"]!.GetValue<string>();
            repository.ShouldBe("other/library");
            var id = current["id"]!.GetValue<string>();
            id.ShouldBe("RM-018");
            var order = current["order"]!.GetValue<int>();
            order.ShouldBe(11);
            var previous = github["previousLocations"]![0]!["issue"]!.GetValue<int>();
            previous.ShouldBe(100);
            RepoConfigVerifier.Verify(workspace.RootPath).ShouldBeEmpty();
            var state = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
            using var repeatHandler = new TestHttpMessageHandler();
            repeatHandler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([]));
            repeatHandler.EnqueueJson(System.Net.HttpStatusCode.OK, """{"data":{"node":{"__typename":"Issue","id":"I_external","number":18,"repository":{"nameWithOwner":"other/library"}}}}""");
            repeatHandler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
            using var repeatClient = new HttpClient(repeatHandler);
            var repeatExit = await RepoConfigToolRunner.Run(arguments, output, error, workspace.RootPath, repeatClient, TestContext.Current.CancellationToken);
            repeatExit.ShouldBe(0);
            var repeatedState = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
            repeatedState.ShouldBe(state);
        }
    }

    /// <summary>Verifies that reconcile reports an inaccessible reference and keeps its reviewed record.</summary>
    [Fact]
    public async Task Reconcile_reports_inaccessible_reference_and_retains_its_reviewed_record()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var original = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapReferenceTestOperations.EnqueueTransfer(handler, unresolved: true);
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldBe(string.Empty);
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("unresolved: RM-018", StringComparison.Ordinal);
        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(original);
    }

    /// <summary>Verifies that reconcile rejects an ambiguous issue identity before writing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_rejects_ambiguous_identity_before_writing(bool nodeCollision)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var original = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        var second = original.Replace("RM-018", "RM-019", StringComparison.Ordinal)
            .Replace("\"order\": 11", "\"order\": 12", StringComparison.Ordinal)
            .Replace("\"issue\": 100", "\"issue\": 101", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-019-github-101.json", second);
        workspace.WriteFile("roadmap/order.json", workspace.ReadFile("roadmap/order.json").Replace("\"RM-018\"", "\"RM-018\", \"RM-019\"", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var snapshot = new GitHubRoadmapReconcileSnapshot(null, [])
        {
            ReferenceResolutions = new Dictionary<string, GitHubIssueReferenceResolution>(StringComparer.Ordinal)
            {
                ["RM-018"] = new("other/library", 18, "I_first", null, null),
                ["RM-019"] = new("other/library", nodeCollision ? 19 : 18, nodeCollision ? "I_first" : "I_second", null, null)
            }
        };
        Action action = () => GitHubRoadmapReferencePlan.Create(project, snapshot);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("multiple roadmap", StringComparison.Ordinal);
        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(original);
        workspace.ReadFile("roadmap/items/RM-019-github-101.json").ShouldBe(second);
    }

    /// <summary>Verifies that a fresh sync skips an unresolved reference and still syncs a healthy item.</summary>
    [Theory]
    [InlineData(System.Net.HttpStatusCode.NotFound, false)]
    [InlineData(System.Net.HttpStatusCode.Gone, false)]
    [InlineData(System.Net.HttpStatusCode.MovedPermanently, false)]
    [InlineData(System.Net.HttpStatusCode.OK, false)]
    [InlineData(System.Net.HttpStatusCode.Gone, true)]
    [InlineData(System.Net.HttpStatusCode.MovedPermanently, true)]
    [InlineData(System.Net.HttpStatusCode.Forbidden, true)]
    public async Task Fresh_sync_skips_unresolved_reference_and_continues_healthy_item(System.Net.HttpStatusCode status, bool preflight)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var original = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        var second = original.Replace("RM-018", "RM-019", StringComparison.Ordinal)
            .Replace("\"order\": 11", "\"order\": 12", StringComparison.Ordinal)
            .Replace("\"issue\": 100", "\"issue\": 101", StringComparison.Ordinal)
            .Replace("\"labels\": []", "\"labels\": [\"area: tooling\"]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-019-github-101.json", second);
        workspace.WriteFile("roadmap/order.json", workspace.ReadFile("roadmap/order.json").Replace("\"RM-018\"", "\"RM-018\", \"RM-019\"", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        if (!preflight)
        {
            handler.EnqueueJson(System.Net.HttpStatusCode.NotFound, "{}");
        }

        handler.EnqueueJson(status, "{}");
        handler.EnqueueJson(System.Net.HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(System.Net.HttpStatusCode.OK, """{"number":101,"node_id":"I_healthy","html_url":"https://github.com/owner/repository/issues/101","labels":[]}""");
        handler.EnqueueJson(System.Net.HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var project = RoadmapProject.Load(workspace.RootPath);
        var state = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Act
        var result = await new GitHubRoadmapSyncer(project, client).Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain(message => message.Contains("unresolved: RM-018", StringComparison.Ordinal));
        result.Messages.ShouldContain("updated labels for owner/repository#101 from RM-019");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Post && request.PathAndQuery.Contains("/100/", StringComparison.Ordinal));
        var after = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        after.ShouldBe(state);
    }

    /// <summary>Verifies that a fresh sync verifies a changed node identifier and accepts a valid format migration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fresh_sync_verifies_changed_node_id_without_rejecting_valid_format_migration(bool transferred)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var original = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("\"issue\": 100", "\"issue\": 100, \"nodeId\": \"I_current\"", StringComparison.Ordinal)
            .Replace("\"labels\": []", "\"labels\": [\"area: tooling\"]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-018-github-100.json", original);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(System.Net.HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(System.Net.HttpStatusCode.OK, """{"number":100,"node_id":"I_legacy","html_url":"https://github.com/owner/repository/issues/100","labels":[]}""");
        handler.EnqueueJson(System.Net.HttpStatusCode.OK, transferred
            ? """{"data":{"node":{"__typename":"Issue","id":"I_current","number":18,"repository":{"nameWithOwner":"other/library"}}}}"""
            : """{"data":{"node":{"__typename":"Issue","id":"I_current","number":100,"repository":{"nameWithOwner":"owner/repository"}}}}""");
        if (!transferred)
        {
            handler.EnqueueJson(System.Net.HttpStatusCode.OK, "{}");
        }

        using var client = new HttpClient(handler);
        var project = RoadmapProject.Load(workspace.RootPath);

        // Act
        var result = await new GitHubRoadmapSyncer(project, client).Apply(TestContext.Current.CancellationToken);

        // Assert
        if (transferred)
        {
            result.Messages.ShouldContain(message => message.Contains("unresolved: RM-018", StringComparison.Ordinal));
            handler.Requests.ShouldNotContain(request => request.PathAndQuery.Contains("/labels", StringComparison.Ordinal));
        }
        else
        {
            result.Messages.ShouldContain("updated labels for owner/repository#100 from RM-018");
            handler.Requests.ShouldContain(request => request.Method == HttpMethod.Post && request.PathAndQuery.EndsWith("/labels", StringComparison.Ordinal));
        }

        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(original);
    }

    /// <summary>Verifies that import requires an exact mapping for an external blocker and keeps the inverse links.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_requires_exact_external_blocker_mapping_and_preserves_inverse_links(bool mapped)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var external = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("\"issue\": 100", mapped ? "\"issue\": 18, \"repository\": \"other/library\"" : "\"issue\": 19, \"repository\": \"other/library\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-018-github-100.json", external);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "Blocked local work", "OPEN", blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(18, "OPEN", "other/library")])
        ];
        const string manifestPath = "roadmap/reconciliation/open-issues-test.json";
        var manifest = JsonNode.Parse(workspace.ReadFile(manifestPath))!.AsObject();
        manifest["needsHuman"] = new JsonArray(101);
        manifest["integrity"]!["expectedIssueCount"] = 1;
        manifest["integrity"]!["expectedNeedsHumanCount"] = 1;
        manifest["integrity"]!["expectedExternalBlockerEdgeCount"] = 1;
        manifest["externalBlockerEdges"] = JsonNode.Parse("""[{"blocker":{"repository":"other/library","issue":18,"state":"OPEN"},"blocked":{"repository":"owner/repository","issue":101,"state":"OPEN"}}]""");
        GitHubRoadmapSnapshotTestOperations.Configure(workspace, manifest.ToJsonString(), snapshot);
        var state = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        if (!mapped)
        {
            exitCode.ShouldBe(1);
            error.ToString().ShouldContain("other/library#18 requires an accessible exact roadmap mapping", StringComparison.Ordinal);
            var after = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
            after.ShouldBe(state);
        }
        else
        {
            error.ToString().ShouldBe(string.Empty);
            exitCode.ShouldBe(0);
            var local = JsonNode.Parse(workspace.ReadFile("roadmap/items/RM-019-github-101.json"))!;
            var blocker = local["blockedBy"]![0]!.GetValue<string>();
            blocker.ShouldBe("RM-018");
            var impact = local["scoring"]!["impact"]!.GetValue<decimal>();
            impact.ShouldBe(2);
            var remote = JsonNode.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"))!;
            var blocked = remote["blocks"]![0]!.GetValue<string>();
            blocked.ShouldBe("RM-019");
            var order = remote["order"]!.GetValue<int>();
            order.ShouldBe(11);
            RepoConfigVerifier.Verify(workspace.RootPath).ShouldBeEmpty();
        }
    }

    /// <summary>Verifies that import rejects an invalid graph that runs through a preserved external record, before it writes.</summary>
    [Theory]
    [InlineData("blocker")]
    [InlineData("parent")]
    [InlineData("closed")]
    public async Task Import_rejects_invalid_graph_through_preserved_external_record_before_writing(string scenario)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialization = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialization.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var template = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        var external = JsonNode.Parse(template)!;
        external["integrations"]!["github"]!["issue"] = 18;
        external["integrations"]!["github"]!["repository"] = "other/library";
        var local = JsonNode.Parse(template)!;
        local["id"] = "RM-019";
        local["order"] = 12;
        local["integrations"]!["github"]!["issue"] = 101;
        if (scenario == "blocker")
        {
            external["blocks"] = new JsonArray("RM-019");
            local["blockedBy"] = new JsonArray("RM-018");
        }
        else if (scenario == "parent")
        {
            external["parent"] = "RM-019";
        }
        else
        {
            external["status"] = "done";
            external["triage"] = "untriaged";
            external.AsObject().Remove("order");
            external.AsObject().Remove("scoring");
        }

        workspace.WriteFile("roadmap/items/RM-018-github-100.json", external.ToJsonString());
        workspace.WriteFile("roadmap/items/RM-019-github-101.json", local.ToJsonString());
        workspace.WriteFile("roadmap/order.json", scenario == "closed"
            ? """{"items":["RM-001","RM-019"]}"""
            : """{"items":["RM-001","RM-018","RM-019"]}""");
        var remote = GitHubRoadmapSnapshotTestOperations.Relation(18, "OPEN", "other/library");
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "Local work", "OPEN",
                parent: scenario == "parent" ? remote : null,
                blocking: scenario == "blocker" ? [remote] : [],
                blockedBy: scenario == "closed" ? [remote] : [])
        ];
        var manifest = JsonNode.Parse(workspace.ReadFile("roadmap/reconciliation/open-issues-test.json"))!.AsObject();
        manifest["needsHuman"] = new JsonArray(101);
        manifest["integrity"]!["expectedIssueCount"] = 1;
        manifest["integrity"]!["expectedNeedsHumanCount"] = 1;
        if (scenario != "parent")
        {
            manifest["integrity"]!["expectedExternalBlockerEdgeCount"] = 1;
            manifest["externalBlockerEdges"] = JsonNode.Parse(scenario == "blocker"
                ? """[{"blocker":{"repository":"owner/repository","issue":101,"state":"OPEN"},"blocked":{"repository":"other/library","issue":18,"state":"OPEN"}}]"""
                : """[{"blocker":{"repository":"other/library","issue":18,"state":"OPEN"},"blocked":{"repository":"owner/repository","issue":101,"state":"OPEN"}}]""");
        }

        GitHubRoadmapSnapshotTestOperations.Configure(workspace, manifest.ToJsonString(), snapshot);
        var state = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var client = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain(scenario == "closed" ? "closed roadmap item" : "cycle", StringComparison.OrdinalIgnoreCase);
        var after = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        after.ShouldBe(state);
    }

}
