using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in RepoConfigToolApplicationTests.</summary>
public sealed class GitHubProjectSyncTests
{
    [Fact]
    public async Task GitHub_roadmap_syncer_apply_throws_timeout_when_issue_creation_limit_is_reached()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new InvariantStringWriter();
        using var initError = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);
        var syncer = new GitHubRoadmapSyncer(project, connection, timeProvider);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub sync timed out after 30 seconds.");
        workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").ShouldContain("\"issue\": \"create\"", StringComparison.Ordinal);
        var lockPath = Path.Combine(workspace.RootPath, "roadmap/items/RM-001-roadmap-gitops.json.lock");
        File.Exists(lockPath).ShouldBeTrue();
        using var retryHandler = new TestHttpMessageHandler();
        using var retryHttpClient = new HttpClient(retryHandler);
        using var retryHttpClientConnection = new GitHubConnection(retryHttpClient);
        var retrySyncer = new GitHubRoadmapSyncer(project, retryHttpClientConnection, timeProvider);
        Func<Task> retry = () => retrySyncer.Apply(TestContext.Current.CancellationToken);
        await retry.ShouldThrow<InvalidOperationException>();
        retryHandler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_project_field_reads_fail_closed_when_graphql_reports_an_error()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"errors\": [{ \"type\": \"FORBIDDEN\" }] }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        Func<Task> action = () => client.GetFieldValues("item-id", TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed (FORBIDDEN).");
    }

    [Fact]
    public async Task GitHub_project_client_finds_existing_item_on_a_later_page()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"other-item\", \"content\": { \"id\": \"other-issue\" } }], \"pageInfo\": { \"hasNextPage\": true, \"endCursor\": \"cursor-1\" } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"target-item\", \"content\": { \"id\": \"target-issue\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var itemId = await client.FindItemId(target, "target-issue", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBe("target-item");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Body.ShouldContain("nodes{id content{... on Issue{id}}}pageInfo{hasNextPage endCursor}", StringComparison.Ordinal);
        handler.Requests[1].Body.ShouldContain("cursor-1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_project_client_pages_item_membership_once_for_many_lookups()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-1\", \"content\": { \"id\": \"issue-1\" } }], \"pageInfo\": { \"hasNextPage\": true, \"endCursor\": \"cursor-1\" } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-2\", \"content\": { \"id\": \"issue-2\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var first = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);
        var second = await client.FindItemId(target, "issue-2", TestContext.Current.CancellationToken);
        var missing = await client.FindItemId(target, "issue-3", TestContext.Current.CancellationToken);

        // Assert
        first.ShouldBe("item-1");
        second.ShouldBe("item-2");
        missing.ShouldBeNull();
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_project_client_remembers_an_added_item_for_later_lookups()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"addProjectV2ItemById\": { \"item\": { \"id\": \"new-item\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var before = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);
        before.ShouldBeNull();
        await client.AddIssue(target, "issue-1", TestContext.Current.CancellationToken);
        var itemId = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBe("new-item");
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_project_client_reads_field_definitions_once_per_target()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [{ \"id\": \"field-1\", \"name\": \"Roadmap score\", \"dataType\": \"NUMBER\" }] } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var first = await client.GetFields(target, TestContext.Current.CancellationToken);
        var second = await client.GetFields(target, TestContext.Current.CancellationToken);

        // Assert
        second.ShouldBeSameAs(first);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_project_target_accepts_case_insensitive_owner_login()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "OWNER", 1);

        // Act
        await client.VerifyTarget(target, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_project_fields_fail_closed_when_graphql_reports_an_error()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"errors\": [{ \"type\": \"FORBIDDEN\" }] }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        Func<Task> action = () => client.GetFields(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed (FORBIDDEN).");
    }

    [Fact]
    public async Task GitHub_project_client_returns_null_when_item_is_not_present()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        var itemId = await client.FindItemId(new GitHubProjectTarget("project-id", "owner", 1), "missing", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBeNull();
    }

    [Fact]
    public async Task GitHub_project_target_rejects_mismatched_remote_number()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 2, \"owner\": { \"login\": \"owner\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.VerifyTarget(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Configured GitHub Project target could not be verified.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_issue_node_ID()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": {} } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetIssueNodeId("owner/repository", 1, TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub issue mapping could not be found: #1.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_HTTP_failure()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Forbidden, "{}");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetFields(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed with HTTP 403.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_field_values()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": {} } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetFieldValues("item-id", TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project field values could not be read.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_project_items()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": {} } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.FindItemId(new GitHubProjectTarget("project-id", "owner", 1), "issue-id", TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project items could not be read.");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_resets_the_timeout_for_each_item()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new InvariantStringWriter();
        using var initError = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", defaultItem.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var followUpItem = RoadmapTestContent.IssueWithGitHubMappingJson.Replace("\"issue\": 997", "\"issue\": 998", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", followUpItem);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimeAdvancingHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);
        var syncer = new GitHubRoadmapSyncer(project, connection, timeProvider);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_throws_timeout_when_the_per_item_limit_is_reached()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new InvariantStringWriter();
        using var initError = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);
        var syncer = new GitHubRoadmapSyncer(project, connection, timeProvider);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub sync timed out after 30 seconds.");
    }
}
