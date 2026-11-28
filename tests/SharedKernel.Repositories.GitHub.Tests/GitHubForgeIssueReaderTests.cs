using System.Net;

namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Verifies the GitHub provider through the forge-neutral issue reader.</summary>
public sealed class GitHubForgeIssueReaderTests
{
    [Fact]
    public async Task Lists_open_issues_with_labels_parent_and_cross_repository_blockers()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                4,
                "Example issue",
                "OPEN",
                labels: ["ready"],
                parent: GitHubRoadmapSnapshotTestOperations.Relation(2, "OPEN"),
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(12, "CLOSED", "owner/other")])
        ]));
        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);

        // Act
        var issues = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);

        // Assert
        var issue = issues.ShouldHaveSingleItem();
        reader.Capabilities.ShouldBe(ForgeIssueCapabilities.StateReason | ForgeIssueCapabilities.Parent | ForgeIssueCapabilities.Blockers);
        issue.Reference.ShouldBe(new ForgeIssueReference("owner/repository", 4));
        issue.Title.ShouldBe("Example issue");
        issue.IsOpen.ShouldBeTrue();
        issue.StateReason.ShouldBeNull();
        issue.Labels.ShouldBe(["ready"]);
        issue.Parent.ShouldBe(new ForgeIssueLink(new ForgeIssueReference("owner/repository", 2), true));
        issue.BlockedBy.ShouldBe([new ForgeIssueLink(new ForgeIssueReference("owner/other", 12), false)]);
    }

    [Fact]
    public async Task Accepts_all_pages_when_the_reported_issue_total_matches()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
            [GitHubRoadmapSnapshotTestOperations.Issue(4, "First issue", "OPEN")], totalCount: 2, nextCursor: "second"));
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
            [GitHubRoadmapSnapshotTestOperations.Issue(12, "Second issue", "OPEN")], totalCount: 2));
        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);

        // Act
        var issues = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);

        // Assert
        issues.Select(issue => issue.Reference.Number).ShouldBe([4L, 12L]);
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(2, 1)]
    [InlineData(-1, null)]
    public async Task Rejects_an_inconsistent_reported_issue_total(int firstTotal, int? secondTotal)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
            [GitHubRoadmapSnapshotTestOperations.Issue(4, "First issue", "OPEN")],
            totalCount: firstTotal, nextCursor: secondTotal.HasValue ? "second" : null));
        if (secondTotal is { } total)
        {
            handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
                [GitHubRoadmapSnapshotTestOperations.Issue(12, "Second issue", "OPEN")], totalCount: total));
        }

        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);
        Func<Task> read = async () => _ = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);

        // Act
        var error = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("issue total", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reads_a_closed_issue_by_repository_and_number()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """
            {
              "data": {
                "repository": {
                  "issueOrPullRequest": {
                    "__typename": "Issue",
                    "number": 12,
                    "title": "Cancelled",
                    "state": "CLOSED",
                    "stateReason": "NOT_PLANNED",
                    "labels": {"nodes": [], "pageInfo": {"hasNextPage": false}},
                    "parent": null,
                    "subIssues": {"nodes": [], "pageInfo": {"hasNextPage": false}},
                    "blockedBy": {"nodes": [], "pageInfo": {"hasNextPage": false}},
                    "blocking": {"nodes": [], "pageInfo": {"hasNextPage": false}}
                  }
                }
              }
            }
            """);
        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);
        var reference = new ForgeIssueReference("owner/other", 12);

        // Act
        var issue = await reader.ReadIssue(reference, TestContext.Current.CancellationToken);

        // Assert
        issue.Reference.ShouldBe(reference);
        issue.IsOpen.ShouldBeFalse();
        issue.StateReason.ShouldBe("NOT_PLANNED");
    }

    [Fact]
    public async Task Missing_relationship_metadata_is_reported_as_incomplete()
    {
        // Arrange
        using var response = GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
            [GitHubRoadmapSnapshotTestOperations.Issue(4, "Example issue", "OPEN")]);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, json.Replace("\"parent\":null,", string.Empty, StringComparison.Ordinal));
        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);

        // Act
        Func<Task> action = async () => _ = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);
        var error = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("owner/repository", StringComparison.Ordinal);
        error.Message.ShouldContain("parent", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"data":{"repository":{"issueOrPullRequest":null}}}""")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Forbidden"}""")]
    public async Task An_inaccessible_issue_raises_a_diagnostic(HttpStatusCode status, string response)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(status, response);
        using var http = new HttpClient(handler);
        var reader = new GitHubForgeIssueReader(http);

        // Act
        Func<Task> action = async () => _ = await reader.ReadIssue(new ForgeIssueReference("owner/other", 12), TestContext.Current.CancellationToken);
        var error = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("owner/other#12", StringComparison.Ordinal);
    }
}
