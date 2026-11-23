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
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(Releases.Linked(Releases.Page(1, 50), """<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2>; rel="next",<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2>; rel="last" """));
        handler.Enqueue(Releases.Linked(Releases.Page(51, 50), """<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1>; rel="first",<https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1>; rel="prev" """));
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(100);
        releases[0].ShouldBe(new RepositoryRelease(1, "v1.0.0-alpha.1", false, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        handler.Requests.Select(static request => request.Message.RequestUri!.ToString()).ShouldBe(
        [
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1",
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2",
        ]);
        handler.Requests[0].Message.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("token", "secret"));
    }

    [Fact]
    public async Task Listing_without_a_link_header_reads_pages_until_the_total_count()
    {
        // Arrange
        // Forgejo 17 answers some lists, such as the labels of a repository, with X-Total-Count and no Link header.
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(Releases.Counted(Releases.Page(1, 50), 60));
        handler.Enqueue(Releases.Counted(Releases.Page(51, 10), 60));
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(60);
        handler.Requests.Select(static request => request.Message.RequestUri!.ToString()).ShouldBe(
        [
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1",
            "https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=2",
        ]);
    }

    [Fact]
    public async Task Without_paging_headers_the_list_ends_at_an_empty_page()
    {
        // Arrange
        // A short page proves nothing without headers: the instance can page by fewer than 50, so an empty page ends the list.
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Releases.Page(1, 3));
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        var releases = await store.List(TestContext.Current.CancellationToken);

        // Assert
        releases.Count.ShouldBe(3);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Message.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task A_refused_listing_names_the_request_and_the_status()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

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
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, json);

        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Act
        var exception = await list.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldStartWith("GET https://forge.example/api/v1/repos/owner/name/releases?limit=50&page=1 returned a release list that is not valid: ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_release_list_longer_than_two_hundred_pages_fails_as_invalid_data_through_the_release_store()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var page = 0; page < 200; page++)
        {
            handler.Enqueue(Releases.Counted(Releases.Page((page * 50) + 1, 50), 20_000));
        }

        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, null);
        Func<Task> list = () => ((IReleaseStore)store).List(TestContext.Current.CancellationToken);

        // Act
        var exception = await list.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldBe("The releases of owner/name span more than 200 pages of 50 items; nothing was read from a partial list.");
        handler.Requests.Count.ShouldBe(200);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/..")]
    public void A_repository_that_is_not_owner_slash_name_is_rejected(string repository)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        Action create = () => new ForgejoReleaseStore(handler, Server, repository, UserAgent, null).Dispose();

        // Assert
        var exception = create.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("repository");
        exception.Message.ShouldBe("The repository must be owner/name, each part of ASCII letters, digits, '-', '.', and '_', and neither part '.' nor '..'. (Parameter 'repository')");
    }

    [Fact]
    public async Task Deleting_a_release_by_its_ID_accepts_only_no_content()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
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
            request.Message.RequestUri!.ToString().ShouldBe("https://forge.example/api/v1/repos/owner/name/releases/42");
        });
        exception.Message.ShouldBe("DELETE https://forge.example/api/v1/repos/owner/name/releases/42 returned 403 Forbidden.");
        exception.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pruning_through_the_store_deletes_only_the_older_prereleases()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Releases.Page(1, 3));
        handler.EnqueueJson(HttpStatusCode.OK, "[]");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var store = new ForgejoReleaseStore(handler, Server, "owner/name", UserAgent, "secret");
        using var output = new InvariantStringWriter();

        // Act
        await PrereleasePruner.Prune(new PrereleasePruningRequest("owner/name", 2, [], Force: true), store, output, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests[^1].Message.RequestUri!.ToString().ShouldBe("https://forge.example/api/v1/repos/owner/name/releases/1");
        output.ToString().ShouldContain("Deleted 1 prerelease; their tags stay.", StringComparison.Ordinal);
    }
}
