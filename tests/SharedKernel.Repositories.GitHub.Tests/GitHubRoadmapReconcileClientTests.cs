using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in GitHubRoadmapReconcileTests.</summary>
public sealed class GitHubRoadmapReconcileClientTests
{
    [Fact]
    public async Task Snapshot_rejects_default_branch_movement_between_issue_pages()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"page-2"}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """);
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rechecks_default_branch_after_all_issue_metadata()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[{"__typename":"Issue","number":100,"title":"Paged relations","state":"OPEN","labels":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}},"parent":null,"subIssues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"relations-2"}},"blockedBy":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}},"blocking":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"issue":{"subIssues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
                """);
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rereads_when_only_the_default_branch_commit_changed()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        var snapshot = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);

        // Assert
        snapshot.RepositoryCommit.ShouldBe("2222222222222222222222222222222222222222");
    }

    [Fact]
    public async Task Snapshot_stops_rereading_after_three_default_branch_changes()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        foreach (var (first, second) in new[] { ('1', '2'), ('3', '4'), ('5', '6') })
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"OID"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """.Replace("OID", new string(first, 40), StringComparison.Ordinal));
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"OID"}}}}}
                """.Replace("OID", new string(second, 40), StringComparison.Ordinal));
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rejects_a_repeated_issue_page_cursor()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"repeated"}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"repeated"}}}}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response repeated an issue page cursor.");
    }

    [Fact]
    public async Task Snapshot_rejects_an_oversized_response()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
        response.Content.Headers.ContentLength = 9 * 1024 * 1024;
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response exceeded the allowed size.");
    }

    [Fact]
    public async Task Snapshot_deadline_cancels_a_stalled_request_without_waiting_in_real_time()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider, TimeSpan.FromMinutes(2));
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient, timeProvider);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation snapshot timed out after 120 seconds.");
    }

    [Fact]
    public async Task Snapshot_preserves_caller_cancellation_instead_of_reporting_a_timeout()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var handler = TestHttpMessageHandler.FromException(new OperationCanceledException(cancellation.Token));
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], cancellation.Token);
        var exception = await action.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Snapshot_rejects_an_oversized_body_even_when_content_length_understates_it()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[(8 * 1024 * 1024) + 1])
        };
        response.Content.Headers.ContentLength = 0;
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response exceeded the allowed size.");
    }
}
