using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.Repositories.Forgejo.Tests;

/// <summary>Verifies how issue reads interpret the Forgejo API's issue and dependency lists.</summary>
public sealed class ForgejoIssueReaderTests
{
    private static readonly Uri Server = new("https://forge.example");
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");
    private const string Issue = """{"number":4,"title":"Example issue","state":"open","labels":[{"name":"ready"}],"repository":{"full_name":"owner/repository"},"pull_request":null}""";

    [Fact]
    public async Task Lists_open_issues_and_reads_cross_repository_blockers()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(LinkedPages.Counted("[" + Issue + "]", 1));
        handler.EnqueueJson(HttpStatusCode.OK, """[{"number":12,"title":"Example blocker","state":"closed","repository":{"full_name":"owner/other"}}]""");
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");

        // Act
        var issues = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);

        // Assert
        reader.Capabilities.ShouldBe(ForgeIssueCapabilities.Blockers);
        var issue = issues.ShouldHaveSingleItem();
        issue.Reference.ShouldBe(new ForgeIssueReference("owner/repository", 4));
        issue.IsOpen.ShouldBeTrue();
        issue.Labels.ShouldBe(["ready"]);
        issue.Parent.ShouldBeNull();
        issue.StateReason.ShouldBeNull();
        issue.BlockedBy.ShouldBe([new ForgeIssueLink(new ForgeIssueReference("owner/other", 12), false)]);
        handler.Requests.Select(static request => request.PathAndQuery).ShouldBe(
        [
            "/api/v1/repos/owner/repository/issues?state=open&type=issues&limit=50&page=1",
            "/api/v1/repos/owner/repository/issues/4/dependencies?limit=50&page=1",
            "/api/v1/repos/owner/repository/issues/4/dependencies?limit=50&page=2"
        ]);
        handler.Requests.ShouldAllSatisfy(static request => request.Method.ShouldBe(HttpMethod.Get));
    }

    [Fact]
    public async Task Reads_a_closed_issue_with_a_large_issue_number()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"number":2147483648,"title":"Closed issue","state":"closed","labels":[],"repository":{"full_name":"owner/other"}}""");
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");
        var reference = new ForgeIssueReference("owner/other", 2147483648);

        // Act
        var issue = await reader.ReadIssue(reference, TestContext.Current.CancellationToken);

        // Assert
        issue.Reference.ShouldBe(reference);
        issue.IsOpen.ShouldBeFalse();
        issue.BlockedBy.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_empty_open_page_after_advertised_continuation_is_incomplete(bool useCount)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        var first = "[" + Issue + "]";
        handler.Enqueue(useCount
            ? LinkedPages.Counted(first, 2)
            : LinkedPages.Json(first, """<https://forge.example/api/v1/repos/owner/repository/issues?page=2>; rel="next" """));
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");

        // Act
        Func<Task> read = async () => _ = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);
        var error = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("owner/repository", StringComparison.Ordinal);
        error.Message.ShouldContain("incomplete", StringComparison.OrdinalIgnoreCase);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Blocking_issue_pages_that_change_the_advertised_count_are_incomplete()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Issue);
        handler.Enqueue(LinkedPages.Counted("""[{"number":12,"state":"closed","repository":{"full_name":"owner/other"}}]""", 2));
        handler.Enqueue(LinkedPages.Counted("""[{"number":13,"state":"open","repository":{"full_name":"owner/other"}}]""", 1));
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");
        var reference = new ForgeIssueReference("owner/repository", 4);

        // Act
        Func<Task> read = async () => _ = await reader.ReadIssue(reference, TestContext.Current.CancellationToken);
        var error = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("blockers for owner/repository#4", StringComparison.Ordinal);
        error.Message.ShouldContain("incomplete", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task The_open_issue_count_must_match_the_completed_page_sequence(int advertisedCount)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        var page = LinkedPages.Counted("[" + Issue + "]", advertisedCount);
        page.Headers.Add("Link", """<https://forge.example/api/v1/repos/owner/repository/issues?page=1>; rel="last" """);
        handler.Enqueue(page);
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");

        // Act
        Func<Task> read = async () => _ = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);
        var error = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("incomplete", StringComparison.OrdinalIgnoreCase);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Listings_without_headers_include_every_page_until_the_empty_page()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "[" + Issue + "]");
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, token: null);

        // Act
        var issues = await reader.ReadOpenIssues("owner/repository", TestContext.Current.CancellationToken);

        // Assert
        issues.ShouldHaveSingleItem().Reference.ShouldBe(new ForgeIssueReference("owner/repository", 4));
        handler.Requests.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
    [InlineData(HttpStatusCode.OK, """[{"number":0,"title":"HIDDEN","state":"","repository":null}]""")]
    public async Task Unavailable_blockers_raise_a_diagnostic(HttpStatusCode status, string response)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Issue);
        handler.EnqueueJson(status, response);
        using var reader = new ForgejoIssueReader(handler, Server, UserAgent, "token");

        // Act
        Func<Task> action = async () => _ = await reader.ReadIssue(new ForgeIssueReference("owner/repository", 4), TestContext.Current.CancellationToken);
        var error = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("owner/repository#4", StringComparison.Ordinal);
        error.Message.ShouldContain("blocker", StringComparison.Ordinal);
    }
}
