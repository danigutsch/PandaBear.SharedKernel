using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the GitHub roadmap publisher's tree, commit, reference, and pull request requests and the checks it makes between them.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapGitHubPublisherTests
{
    private const string PullRequestBody = "Refs #42";

    /// <summary>Publishing one changed file sends a tree on the base tree with a single <c>100644</c> entry, a commit with the base SHA as sole parent and no author, committer, or signature, a non-forced branch reference, and a draft pull request to <c>main</c> with the given body, without any PATCH request.</summary>
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
        var publication = await publisher.Publish(plan, plan.BaseSha, changes, PullRequestBody, TestContext.Current.CancellationToken);
        var treeRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery.EndsWith("/git/trees", StringComparison.Ordinal));
        var commitRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery.EndsWith("/git/commits", StringComparison.Ordinal));
        var refRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal));
        var pullRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal));
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
        pullRequestBody.ShouldBe(PullRequestBody);
        handler.Requests.Any(request => request.Message.Method == HttpMethod.Patch).ShouldBeFalse();
    }

    /// <summary>When GitHub answers the reference creation with 422 "Reference already exists", publishing throws that it could not create a new branch and sends neither a PATCH nor a pull request.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication could not create a new branch; the ref already exists or was rejected.");
        handler.Requests.Any(request => request.Message.Method == HttpMethod.Patch).ShouldBeFalse();
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>Publishing a rename inside <c>roadmap/items</c> sends a tree with two entries: the old path <c>RM-100.json</c> with a null SHA to delete it, and the new path <c>RM-101.json</c> with the created blob SHA.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var treeRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery.EndsWith("/git/trees", StringComparison.Ordinal));
        using var treeBody = JsonDocument.Parse(treeRequest.Body.ShouldNotBeNull());
        var entries = treeBody.RootElement.GetProperty("tree");

        // Assert
        entries.GetArrayLength().ShouldBe(2);
        entries[0].GetProperty("path").GetString().ShouldBe("roadmap/items/RM-100.json");
        entries[0].GetProperty("sha").ValueKind.ShouldBe(JsonValueKind.Null);
        entries[1].GetProperty("path").GetString().ShouldBe("roadmap/items/RM-101.json");
        entries[1].GetProperty("sha").GetString().ShouldBe(RoadmapGitHubPublicationTestOperations.BlobSha);
    }

    /// <summary>When the default branch no longer points at the base SHA after the objects are created, publishing throws that the branch moved after result verification and creates no reference.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication default branch moved after result verification.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>When the default branch has moved to a different SHA once the reference was created, publishing throws that the branch moved during branch creation and opens no pull request.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication default branch moved during branch creation.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>When GitHub reports the created commit as unverified, publishing throws that a GitHub-verified App commit is required and creates no reference.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication requires a GitHub-verified App commit.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>When the verified commit reports a tree SHA other than the created tree, publishing throws that the commit does not match the approved tree and sole parent and creates no reference.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication commit does not match the approved tree and sole parent.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>When the verified commit lists two parents, publishing throws that the commit does not match the approved tree and sole parent and creates no reference.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication commit does not match the approved tree and sole parent.");
        handler.Requests.Any(request => request.PathAndQuery.EndsWith("/git/refs", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>Publishing a change set of 513 paths throws that it exceeds the allowed changed-path count and sends no HTTP request.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication exceeds the allowed changed-path count.");
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Previewing a change set of 513 paths throws the same exceeds-the-allowed-changed-path-count error as publishing.</summary>
    [Fact]
    public async Task Preview_applies_the_publication_path_count_limit()
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

        // Act
        var action = () => RoadmapGitHubPublisher.Preview(
            workspace.RootPath,
            RoadmapAutomationTestPlan.Create(),
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication exceeds the allowed changed-path count.");
    }

    /// <summary>Previewing a rename inside <c>roadmap/items</c> reports the repository, base SHA, branch name, and both the old and new paths.</summary>
    [Fact]
    public async Task Preview_names_both_paths_of_a_confined_rename()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/items/RM-101.json", "{}\n");
        var changes = RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Rename("roadmap/items/RM-101.json", "roadmap/items/RM-100.json"));
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        var preview = await RoadmapGitHubPublisher.Preview(
            workspace.RootPath,
            plan,
            plan.BaseSha,
            changes,
            TestContext.Current.CancellationToken);

        // Assert
        preview.Repository.ShouldBe("owner/repository");
        preview.BaseSha.ShouldBe(RoadmapAutomationTestPlan.BaseSha);
        preview.BranchName.ShouldBe(plan.BranchName);
        preview.Paths.ShouldBe(["roadmap/items/RM-100.json", "roadmap/items/RM-101.json"]);
    }

    /// <summary>When the handler advances a fake clock by 10 minutes, publishing throws a <see cref="TimeoutException"/> that names the aggregate limit of 600 seconds.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub roadmap publication timed out after 600 seconds.");
    }

    /// <summary>When the pull request response carries an empty URL, publishing throws that no pull request URL was returned.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication did not return a pull request URL.");
    }

    /// <summary>When the created pull request reports a base SHA other than the approved one, publishing throws that the pull request does not match the approved base and head, after exactly one pull request POST.</summary>
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
            RoadmapAutomationTestPlan.BaseSha,
            changes,
            PullRequestBody,
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap publication pull request does not match the approved base and head.");
        handler.Requests.Count(request => request.Message.Method == HttpMethod.Post
            && request.PathAndQuery.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBe(1);
    }
}
