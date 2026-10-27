using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests how the resolver follows GitHub issue references across transfers and migrations.</summary>
public sealed class GitHubIssueReferenceResolverTests
{
    /// <summary>Verifies that the resolver follows a transfer redirect and keeps the node identity opaque.</summary>
    [Fact]
    public async Task Resolve_follows_transfer_and_keeps_opaque_identity()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        using var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        redirect.Headers.Location = new Uri("https://api.github.com/repositories/123/issues/18");
        handler.Enqueue(redirect);
        handler.EnqueueJson(HttpStatusCode.OK, """{"number":18,"node_id":"I_new","html_url":"https://github.com/owner/library/issues/18"}""");
        using var client = new HttpClient(handler);

        // Act
        var result = await new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved.ShouldBeTrue();
        result.Repository.ShouldBe("owner/library");
        result.Number.ShouldBe(18);
        result.NodeId.ShouldBe("I_new");
        handler.Requests.Count.ShouldBe(2);
    }

    /// <summary>Verifies that the resolver rejects an untrusted redirect target before it sends the next request.</summary>
    [Theory]
    [InlineData("https://evil.example/repos/owner/repo/issues/1")]
    [InlineData("http://api.github.com/repos/owner/repo/issues/1")]
    [InlineData("https://api.github.com/repos/owner/repo/issues/1?token=secret")]
    [InlineData("https://api.github.com/user")]
    public async Task Resolve_rejects_untrusted_redirect_before_sending(string location)
    {
        // Arrange
        using var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        redirect.Headers.Location = new Uri(location);
        using var handler = new TestHttpMessageHandler(redirect);
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);

        // Assert
        await action.ShouldThrow<InvalidOperationException>();
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Verifies that the resolver refreshes a migrated node identifier without decoding it.</summary>
    [Fact]
    public async Task Resolve_refreshes_migrated_node_id_without_decoding()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"node":{"__typename":"Issue","id":"I_new","number":18,"repository":{"nameWithOwner":"owner/library"}}}}""");
        using var client = new HttpClient(handler);

        // Act
        var result = await new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, "opaque-old", TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved.ShouldBeTrue();
        result.PreviousNodeId.ShouldBe("opaque-old");
        result.NodeId.ShouldBe("I_new");
        result.Repository.ShouldBe("owner/library");
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Body.ShouldContain("opaque-old", StringComparison.Ordinal);
    }

    /// <summary>Verifies that the resolver falls back when the node is missing and reports an inaccessible locator.</summary>
    [Fact]
    public async Task Resolve_falls_back_from_missing_node_and_reports_inaccessible_locator()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"node":null}}""");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        using var client = new HttpClient(handler);

        // Act
        var result = await new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, "opaque-old", TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved.ShouldBeFalse();
        result.Repository.ShouldBe("owner/app");
        result.Number.ShouldBe(457);
        result.Diagnostic.ShouldContain("owner/app#457", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(2);
    }

    /// <summary>Verifies that the resolver rejects metadata that describes a pull request.</summary>
    [Fact]
    public async Task Resolve_rejects_pull_request_metadata()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"number":18,"node_id":"PR_new","html_url":"https://github.com/owner/library/issues/18","pull_request":{}}""");
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);

        // Assert
        await action.ShouldThrow<InvalidOperationException>();
    }
    /// <summary>Verifies that the resolver accepts a response that uses the last request the rate limit allows.</summary>
    [Fact]
    public async Task Resolve_accepts_the_last_successful_rate_limit_allowance()
    {
        // Arrange
        using var response = HttpResponseFactory.Json("""{"number":18,"node_id":"I_new","html_url":"https://github.com/owner/library/issues/18"}""");
        response.Headers.Add("X-RateLimit-Remaining", "0");
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler);

        // Act
        var result = await new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved.ShouldBeTrue();
        result.Number.ShouldBe(18);
    }

    /// <summary>Verifies that a deleted issue stays in the result as an unresolved reference.</summary>
    [Fact]
    public async Task Resolve_preserves_deleted_issue_as_unresolved()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Gone, "{}");
        using var client = new HttpClient(handler);

        // Act
        var result = await new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsResolved.ShouldBeFalse();
        result.Repository.ShouldBe("owner/app");
        result.Number.ShouldBe(457);
        result.Diagnostic.ShouldContain("410", StringComparison.Ordinal);
    }

    /// <summary>Verifies that the resolver rejects a redirect loop.</summary>
    [Fact]
    public async Task Resolve_rejects_redirect_loops()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        response.Headers.Location = new Uri("https://api.github.com/repos/owner/app/issues/457");
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("loop", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Verifies that cancellation by the caller propagates instead of being reported as an unresolved reference.</summary>
    [Fact]
    public async Task Resolve_does_not_swallow_caller_cancellation()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, cancellation.Token);
        await action.ShouldThrow<OperationCanceledException>();

        // Assert
        handler.Requests.Count.ShouldBe(0);
    }

    /// <summary>Verifies that the resolver limits how many response bytes it reads.</summary>
    [Fact]
    public async Task Resolve_bounds_response_size()
    {
        // Arrange
        using var response = HttpResponseFactory.Json("{}");
        response.Content.Headers.ContentLength = 2 * 1024 * 1024;
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("exceeded the allowed size", StringComparison.Ordinal);
    }

    /// <summary>Verifies that the resolver stops when the snapshot deadline passes.</summary>
    [Fact]
    public async Task Resolve_enforces_snapshot_deadline()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider, TimeSpan.FromMinutes(2));
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client, timeProvider).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldContain("120 seconds", StringComparison.Ordinal);
    }

    /// <summary>Verifies that the resolver reports rate limiting as a failure instead of treating the reference as unresolved.</summary>
    [Fact]
    public async Task Resolve_surfaces_rate_limiting_instead_of_quarantining_it()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.Add("X-RateLimit-Remaining", "0");
        using var handler = new TestHttpMessageHandler(response);
        using var client = new HttpClient(handler);

        // Act
        Func<Task> action = () => new GitHubIssueReferenceResolver(client).Resolve("owner/app", 457, null, TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("rate limited", StringComparison.Ordinal);
    }

}
