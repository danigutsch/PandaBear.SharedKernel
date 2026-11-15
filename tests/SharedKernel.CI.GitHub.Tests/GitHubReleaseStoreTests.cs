using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.CI.GitHub.Tests;

/// <summary>Guards how the GitHub release store lists and deletes releases through the REST API.</summary>
public sealed class GitHubReleaseStoreTests
{
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");

    [Fact]
    public async Task Listing_reads_pages_of_100_until_a_short_page_with_the_API_headers_and_token()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, Releases.Page(1, 100)).Respond(HttpStatusCode.OK, Releases.Page(101, 2));
        using var store = new GitHubReleaseStore(handler, "owner/name", UserAgent, "secret");

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(102);
        releases[0].ShouldBe(new RepositoryRelease(1, "v1.0.0-alpha.1", false, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        handler.Requests.Select(static request => request.RequestUri!.ToString()).ShouldBe(
        [
            "https://api.github.com/repos/owner/name/releases?per_page=100&page=1",
            "https://api.github.com/repos/owner/name/releases?per_page=100&page=2",
        ]);
        handler.Requests[0].Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "secret"));
        handler.Requests[0].Headers.UserAgent.ToString().ShouldBe("sharedkernel-test/1.0");
    }

    [Fact]
    public async Task A_refused_listing_names_the_request_and_the_status()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.Forbidden);

        using var store = new GitHubReleaseStore(handler, "owner/name", UserAgent, null);

        // Act
        Func<Task> action = () => store.List(TestContext.Current.CancellationToken);

        var exception = await action.ShouldThrow<HttpRequestException>();

        // Assert
        exception.Message.ShouldBe("GET https://api.github.com/repos/owner/name/releases?per_page=100&page=1 returned 403 Forbidden.");
        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("[null]", "a release is null.")]
    [InlineData("""[{"id":1,"tag_name":"v1","draft":false,"prerelease":true}]""", "")]
    public async Task A_release_list_that_is_not_valid_is_refused(string json, string detail)
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, json);

        using var store = new GitHubReleaseStore(handler, "owner/name", UserAgent, null);

        // Act
        Func<Task> action = () => store.List(TestContext.Current.CancellationToken);

        var exception = await action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldStartWith("GET https://api.github.com/repos/owner/name/releases?per_page=100&page=1 returned a release list that is not valid: ", StringComparison.Ordinal);
        exception.Message.ShouldEndWith(detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_a_release_by_its_ID_accepts_only_no_content()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.NoContent).Respond(HttpStatusCode.NotFound);
        using var store = new GitHubReleaseStore(handler, "owner/name", UserAgent, "secret");
        var release = new RepositoryRelease(42, "v1.0.0-alpha.1", false, true, DateTimeOffset.UnixEpoch);

        // Act
        await store.Delete(release, TestContext.Current.CancellationToken);
        Func<Task> action = () => store.Delete(release, TestContext.Current.CancellationToken);

        var exception = await action.ShouldThrow<HttpRequestException>();

        // Assert
        handler.Requests.ShouldAllSatisfy(static request =>
        {
            request.Method.ShouldBe(HttpMethod.Delete);
            request.RequestUri!.ToString().ShouldBe("https://api.github.com/repos/owner/name/releases/42");
        });
        exception.Message.ShouldBe("DELETE https://api.github.com/repos/owner/name/releases/42 returned 404 NotFound.");
        exception.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_lost_connection_while_deleting_names_the_request()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(static (_, _) => throw new HttpRequestException("connection reset"));
        using var store = new GitHubReleaseStore(handler, "owner/name", UserAgent, "secret");
        Func<Task> delete = () => store.Delete(new RepositoryRelease(7, "v1", false, true, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);

        // Act
        var exception = await delete.ShouldThrow<HttpRequestException>();

        // Assert
        exception.Message.ShouldBe("DELETE https://api.github.com/repos/owner/name/releases/7 failed: connection reset");
        exception.StatusCode.ShouldBeNull();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/name/extra")]
    [InlineData("owner/..")]
    [InlineData("own er/name")]
    public void A_repository_that_is_not_owner_and_name_is_refused(string repository)
    {
        // Arrange
        using var handler = new ScriptedHandler();

        // Act
        var create = () => new GitHubReleaseStore(handler, repository, UserAgent, null);

        // Assert
        create.ShouldThrow<ArgumentException>().ParamName.ShouldBe("repository");
    }
}
