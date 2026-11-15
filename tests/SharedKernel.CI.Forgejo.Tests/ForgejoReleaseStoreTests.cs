using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.CI.Forgejo.Tests;

/// <summary>Guards how the Forgejo release store lists and deletes releases through the API.</summary>
public sealed class ForgejoReleaseStoreTests
{
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");
    private static readonly Uri Server = new("https://forge.example");

    [Fact]
    public async Task Listing_follows_the_next_link_until_the_last_page()
    {
        // Arrange
        using var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.OK, Releases.Page(1, 50), """<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2>; rel="next",<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2>; rel="last" """)
            .Respond(HttpStatusCode.OK, Releases.Page(51, 50), """<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1>; rel="first",<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1>; rel="prev" """);
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(100);
        releases[0].ShouldBe(new RepositoryRelease(1, "v1.0.0-alpha.1", false, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        handler.Requests.Select(static request => request.RequestUri!.ToString()).ShouldBe(
        [
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1",
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2",
        ]);
        handler.Requests[0].Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("token", "secret"));
    }

    [Fact]
    public async Task A_single_page_without_a_link_header_is_the_whole_list()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, Releases.Page(1, 3));
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(3);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task A_refused_listing_names_the_request_and_the_status()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.NotFound);

        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Act
        var exception = await list.ShouldThrow<HttpRequestException>();

        // Assert
        exception.Message.ShouldBe("GET https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1 returned 404 NotFound.");
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("""[{"id":1,"tag_name":"v1","draft":false,"prerelease":true}]""")]
    [InlineData("{}")]
    public async Task A_release_list_that_is_not_valid_is_refused(string json)
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, json);

        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Act
        var exception = await list.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldStartWith("GET https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1 returned a release list that is not valid: ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_a_release_by_its_ID_accepts_only_no_content()
    {
        // Arrange
        using var handler = new ScriptedHandler().Respond(HttpStatusCode.NoContent).Respond(HttpStatusCode.Forbidden);
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, "secret");
        var release = new RepositoryRelease(42, "v1.0.0-alpha.1", false, true, DateTimeOffset.UnixEpoch);
        Func<Task> refused = () => store.Delete(release, TestContext.Current.CancellationToken);

        // Act
        await store.Delete(release, TestContext.Current.CancellationToken);
        var exception = await refused.ShouldThrow<HttpRequestException>();

        // Assert
        handler.Requests.ShouldAllSatisfy(static request =>
        {
            request.Method.ShouldBe(HttpMethod.Delete);
            request.RequestUri!.ToString().ShouldBe("https://forge.example/api/v1/repos/owner/name/releases/42");
        });
        exception.Message.ShouldBe("DELETE https://forge.example/api/v1/repos/owner/name/releases/42 returned 403 Forbidden.");
        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pruning_through_the_store_deletes_only_the_older_prereleases()
    {
        // Arrange
        using var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.OK, Releases.Page(1, 3))
            .Respond(HttpStatusCode.NoContent);
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, "secret");
        using var output = new StringWriter();

        // Act
        await PrereleasePruner.Prune(new PrereleasePruningRequest("owner/name", 2, [], Force: true), store, output, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Last().RequestUri!.ToString().ShouldBe("https://forge.example/api/v1/repos/owner/name/releases/1");
        output.ToString().ShouldContain("Deleted 1 prerelease; their tags stay.", StringComparison.Ordinal);
    }
}
