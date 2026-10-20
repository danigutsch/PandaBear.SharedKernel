using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapGitHubPublisherTests
{
    [Fact]
    public async Task Publication_creates_a_signed_exact_base_commit_and_a_new_draft_pull_request()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        var plan = RoadmapAutomationTestPlan.Create();
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(handler);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        var publication = await publisher.Publish(plan, changes, TestContext.Current.CancellationToken);
        var treeRequest = handler.Requests.Single(request => request.PathAndQuery.EndsWith("/git/trees", StringComparison.Ordinal));
        var commitRequest = handler.Requests.Single(request => request.PathAndQuery.EndsWith("/git/commits", StringComparison.Ordinal));
        var refRequest = handler.Requests.Single(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal));
        var pullRequest = handler.Requests.Single(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal));
        using var treeBody = JsonDocument.Parse(treeRequest.Body.ShouldNotBeNull());
        using var commitBody = JsonDocument.Parse(commitRequest.Body.ShouldNotBeNull());
        using var refBody = JsonDocument.Parse(refRequest.Body.ShouldNotBeNull());
        using var pullBody = JsonDocument.Parse(pullRequest.Body.ShouldNotBeNull());
        var pullRequestBody = pullBody.RootElement.GetProperty("body").GetString();

        // Assert
        publication.PullRequestUrl.ShouldBe(RoadmapGitHubPublicationTestOperations.PullRequestUrl);
        treeBody.RootElement.GetProperty("base_tree").GetString().ShouldBe(RoadmapGitHubPublicationTestOperations.BaseTreeSha);
        var treeEntries = treeBody.RootElement.GetProperty("tree");
        treeEntries.GetArrayLength().ShouldBe(1);
        treeEntries[0].GetProperty("path").GetString().ShouldBe("roadmap/items/RM-100.json");
        treeEntries[0].GetProperty("mode").GetString().ShouldBe("100644");
        treeEntries[0].GetProperty("sha").GetString().ShouldBe(RoadmapGitHubPublicationTestOperations.BlobSha);
        commitBody.RootElement.GetProperty("parents").GetArrayLength().ShouldBe(1);
        commitBody.RootElement.GetProperty("parents")[0].GetString().ShouldBe(RoadmapAutomationTestPlan.BaseSha);
        commitBody.RootElement.TryGetProperty("author", out _).ShouldBeFalse();
        commitBody.RootElement.TryGetProperty("committer", out _).ShouldBeFalse();
        commitBody.RootElement.TryGetProperty("signature", out _).ShouldBeFalse();
        refRequest.Message.Method.ShouldBe(HttpMethod.Post);
        refBody.RootElement.GetProperty("ref").GetString().ShouldBe($"refs/heads/{plan.BranchName}");
        refBody.RootElement.GetProperty("sha").GetString().ShouldBe(RoadmapGitHubPublicationTestOperations.CommitSha);
        refBody.RootElement.TryGetProperty("force", out _).ShouldBeFalse();
        pullBody.RootElement.GetProperty("head").GetString().ShouldBe(plan.BranchName);
        pullBody.RootElement.GetProperty("base").GetString().ShouldBe("main");
        pullBody.RootElement.GetProperty("draft").GetBoolean().ShouldBeTrue();
        pullRequestBody.ShouldBe("Refs #1160");
        handler.Requests.Any(request => request.Message.Method == HttpMethod.Patch).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_atomically_rejects_a_branch_created_after_validation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        handler.EnqueueJson(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Reference already exists\"}");
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication could not create a new branch; the ref already exists or was rejected.");
        handler.Requests.Any(request => request.Message.Method == HttpMethod.Patch).ShouldBeFalse();
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_translates_a_confined_rename_into_one_delete_and_one_blob()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/items/RM-101.json", "{}\n");
        var changes = RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Rename("roadmap/items/RM-101.json", "roadmap/items/RM-100.json"));
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(handler);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var treeRequest = handler.Requests.Single(request => request.PathAndQuery.EndsWith("/git/trees", StringComparison.Ordinal));
        using var treeBody = JsonDocument.Parse(treeRequest.Body.ShouldNotBeNull());
        var entries = treeBody.RootElement.GetProperty("tree");

        // Assert
        entries.GetArrayLength().ShouldBe(2);
        entries[0].GetProperty("path").GetString().ShouldBe("roadmap/items/RM-100.json");
        entries[0].GetProperty("sha").ValueKind.ShouldBe(JsonValueKind.Null);
        entries[1].GetProperty("path").GetString().ShouldBe("roadmap/items/RM-101.json");
        entries[1].GetProperty("sha").GetString().ShouldBe(RoadmapGitHubPublicationTestOperations.BlobSha);
    }

    [Fact]
    public async Task Publication_rejects_default_branch_movement_before_reference_creation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.DifferentSha);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication default branch moved after result verification.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_rejects_default_branch_movement_after_reference_creation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.DifferentSha);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication default branch moved during branch creation.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_rejects_an_unsigned_commit_before_creating_a_branch()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler, verified: false);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication requires a GitHub-verified App commit.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_rejects_a_verified_commit_with_an_unexpected_tree()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(
            handler,
            commitTreeSha: RoadmapGitHubPublicationTestOperations.DifferentSha);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication commit does not match the approved tree and sole parent.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_rejects_a_verified_merge_commit_before_creating_a_branch()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(
            handler,
            parentShas:
            [
                RoadmapGitHubPublicationTestOperations.BaseSha,
                RoadmapGitHubPublicationTestOperations.DifferentSha
            ]);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication commit does not match the approved tree and sole parent.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publication_rejects_an_unbounded_changed_path_set_before_network_access()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var entries = Enumerable.Range(1, 513)
            .Select(number => new RoadmapGitStatusEntry(
                "?",
                "??",
                "N...",
                "000000",
                "000000",
                "100644",
                $"roadmap/items/RM-{number:D3}.json",
                OriginalPath: null))
            .ToArray();
        var changes = new RoadmapGitChangeSet(
            HasChanges: true,
            new string('a', 64),
            entries.Select(entry => entry.Path).ToArray(),
            entries);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication exceeds the allowed changed-path count.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Publication_reports_the_aggregate_timeout_duration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider, TimeSpan.FromMinutes(10));
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient, timeProvider);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub roadmap publication timed out after 600 seconds.");
    }

    [Fact]
    public async Task Publication_rejects_an_empty_pull_request_url()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(handler, url: string.Empty);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication did not return a pull request URL.");
    }

    [Fact]
    public async Task Publication_rejects_a_pull_request_bound_to_a_moved_base()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var changes = RoadmapGitHubPublicationTestOperations.CreateChanges(workspace);
        using var handler = new TestHttpMessageHandler();
        RoadmapGitHubPublicationTestOperations.EnqueueObjectCreation(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueueCreatedReference(handler);
        RoadmapGitHubPublicationTestOperations.EnqueueDefaultBranch(handler, RoadmapGitHubPublicationTestOperations.BaseSha);
        RoadmapGitHubPublicationTestOperations.EnqueuePullRequest(
            handler,
            baseSha: RoadmapGitHubPublicationTestOperations.DifferentSha);
        using var httpClient = new HttpClient(handler);
        var publisher = new RoadmapGitHubPublisher(workspace.RootPath, httpClient);

        // Act
        Func<Task> action = async () => _ = await publisher.Publish(
            RoadmapAutomationTestPlan.Create(),
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication pull request does not match the approved base and head.");
        handler.Requests.Count(request => request.Message.Method == HttpMethod.Post
            && request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBe(1);
    }
}
