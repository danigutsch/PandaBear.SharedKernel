using System.Net;
using System.Text.Json;
using static PandaBear.RepoConfig.Tests.GitHubSubissueTestOperations;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>github link-subissue</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class GitHubSubissueCommandTests
{
    private const string ApiRoot = "https://api.github.com/repos/owner/repository/issues/";

    [Fact]
    public async Task Link_previews_without_network_or_credentials()
    {
        // Arrange
        using var ghToken = new EnvironmentVariableScope("GH_TOKEN", null);
        using var githubToken = new EnvironmentVariableScope("GITHUB_TOKEN", null);
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, output, error) = await Run(handler, force: false);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("preview:", StringComparison.Ordinal);
        output.ShouldContain("owner/repository#20", StringComparison.Ordinal);
        output.ShouldContain("owner/repository#10", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Link_resolves_native_id_and_verifies_the_parent_after_posting()
    {
        // Arrange
        using var handler = Unparented();
        handler.EnqueueJson(HttpStatusCode.Created, Issue(20, 222));
        handler.EnqueueJson(HttpStatusCode.OK, Issue(10, 111));

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("Linked", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(5);
        var post = handler.Requests[3];
        post.Method.ShouldBe(HttpMethod.Post);
        post.Message.RequestUri.ShouldBe(new Uri(ApiRoot + "10/sub_issues"));
        using var body = JsonDocument.Parse(post.Body.ShouldNotBeNull());
        body.RootElement.GetProperty("sub_issue_id").GetInt64().ShouldBe(6000000222);
        body.RootElement.GetProperty("replace_parent").GetBoolean().ShouldBeFalse();
        handler.Requests[4].Message.RequestUri.ShouldBe(new Uri(ApiRoot + "20/parent"));
    }

    [Fact]
    public async Task Link_is_idempotent_when_parent_already_matches()
    {
        // Arrange
        using var handler = Issues();
        handler.EnqueueJson(HttpStatusCode.OK, Issue(10, 111));

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("Already linked", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests.All(request => request.Method == HttpMethod.Get).ShouldBeTrue();
    }

    [Fact]
    public async Task Link_refuses_to_replace_an_existing_parent()
    {
        // Arrange
        using var handler = Issues();
        handler.EnqueueJson(HttpStatusCode.OK, Issue(30, 333));

        // Act
        var (exitCode, _, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("different parent", StringComparison.Ordinal);
        handler.Requests.All(request => request.Method == HttpMethod.Get).ShouldBeTrue();
    }

    [Fact]
    public async Task Link_does_not_claim_success_when_verification_fails()
    {
        // Arrange
        using var handler = Unparented();
        handler.EnqueueJson(HttpStatusCode.Created, Issue(20, 222));
        handler.EnqueueJson(HttpStatusCode.OK, Issue(30, 333));

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("verify", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "ghp_untrusted invalid JSON")]
    public async Task Link_warns_of_a_possible_write_when_verification_response_fails(HttpStatusCode status, string body)
    {
        // Arrange
        using var handler = Unparented();
        handler.EnqueueJson(HttpStatusCode.Created, Issue(20, 6000000222));
        handler.EnqueueJson(status, body);

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("remote write may have succeeded", StringComparison.Ordinal);
        error.ShouldNotContain("ghp_untrusted", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_warns_of_a_possible_write_when_verification_transport_fails()
    {
        // Arrange
        var calls = 0;
        using var handler = new BeforeResponseHttpMessageHandler(
            () => calls switch
            {
                1 => HttpResponseFactory.Json(Issue(10, 111)),
                2 => HttpResponseFactory.Json(Issue(20, 6000000222)),
                3 => new HttpResponseMessage(HttpStatusCode.NotFound),
                _ => new HttpResponseMessage(HttpStatusCode.Created),
            },
            () =>
            {
                calls++;
                if (calls == 5)
                {
                    throw new HttpRequestException("ghp_untrusted_transport");
                }
            });

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("remote write may have succeeded", StringComparison.Ordinal);
        error.ShouldContain("request failed", StringComparison.Ordinal);
        error.ShouldNotContain("ghp_untrusted", StringComparison.Ordinal);
        calls.ShouldBe(5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Link_sanitizes_response_stream_failures_and_preserves_write_uncertainty(bool afterWrite)
    {
        // Arrange
        using var handler = afterWrite ? Unparented() : new TestHttpMessageHandler();
        if (afterWrite)
        {
            handler.EnqueueJson(HttpStatusCode.Created, Issue(20, 6000000222));
        }

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ResponseReadFailureStream()),
        });

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldNotContain("ghp_untrusted", StringComparison.Ordinal);
        error.ShouldContain("request failed", StringComparison.Ordinal);
        error.Contains("remote write may have succeeded", StringComparison.Ordinal).ShouldBe(afterWrite);
    }

    [Fact]
    public async Task Link_accepts_an_already_linked_race_without_repeating_the_write()
    {
        // Arrange
        using var handler = Unparented();
        handler.EnqueueJson(HttpStatusCode.UnprocessableEntity, "{\"message\":\"ghp_untrusted\"}");
        handler.EnqueueJson(HttpStatusCode.OK, Issue(10, 111));

        // Act
        var (exitCode, _, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        handler.Requests.Count(request => request.Method == HttpMethod.Post).ShouldBe(1);
    }

    [Theory]
    [InlineData("owner/repository", "0", "20")]
    [InlineData("owner/repository", "10", "-1")]
    [InlineData("owner/repository", "10", "10")]
    [InlineData("https://evil.example/repository", "10", "20")]
    [InlineData("owner/../repository", "10", "20")]
    public async Task Link_rejects_invalid_identifiers_before_any_request(string repository, string parent, string child)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, _, error) = await Run(handler, repository: repository, parent: parent, child: child);

        // Assert
        exitCode.ShouldBe(1);
        error.Length.ShouldBeGreaterThan(0);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Link_reports_HTTP_errors_without_echoing_remote_content(HttpStatusCode status)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(status, "{\"message\":\"ghp_untrusted\"}");

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain($"HTTP {(int)status}", StringComparison.Ordinal);
        error.ShouldNotContain("ghp_untrusted", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-json ghp_untrusted")]
    [InlineData("{\"id\":0,\"number\":10}")]
    [InlineData("{\"id\":\"ghp_untrusted\",\"number\":10}")]
    [InlineData("{\"id\":111,\"number\":{\"secret\":\"ghp_untrusted\"}}")]
    [InlineData("{\"id\":111,\"id\":222,\"number\":10}")]
    [InlineData("{\"id\":111,\"number\":99,\"url\":\"https://evil.example\"}")]
    [InlineData("{\"id\":111,\"number\":10,\"url\":\"https://api.github.com/repos/owner/repository/issues/10\",\"pull_request\":{}}")]
    public async Task Link_rejects_malformed_or_unexpected_issue_responses(string json)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, json);

        // Act
        var (exitCode, _, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldNotContain("ghp_untrusted", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Link_bounds_response_bytes()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[(1024 * 1024) + 1]),
        });

        // Act
        var (exitCode, _, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("exceeded", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_sanitizes_transport_failures()
    {
        // Arrange
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("ghp_transport"));

        // Act
        var (exitCode, _, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldNotContain("ghp_transport", StringComparison.Ordinal);
        error.ShouldContain("request failed", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_propagates_cancellation_without_a_write()
    {
        // Arrange
        using var handler = TestHttpMessageHandler.FromException(new OperationCanceledException(TestContext.Current.CancellationToken));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        // Act
        var (exitCode, output, _) = await Run(handler, cancellationToken: cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        handler.Requests.ShouldBeEmpty();
    }

}
