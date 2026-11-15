using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Repositories.Forgejo.Tests;

/// <summary>Guards how the Forgejo label store reads, creates, and updates labels through the Forgejo API.</summary>
public sealed class ForgejoLabelStoreTests
{
    private const string LabelsPath = "/api/v1/repos/owner/name/labels";
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");
    private static readonly Uri Server = new("https://forge.example");

    [Fact]
    public async Task Listing_follows_the_next_link_and_returns_colors_without_a_hash()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(LinkedPages.Json(
            """[{ "id": 7, "name": "ci", "color": "1d76db", "description": "Continuous integration", "exclusive": false, "is_archived": false, "url": "u" }]""",
            """<https://forge.example/api/v1/repos/owner/name/labels?limit=50&page=2>; rel="next",<https://forge.example/api/v1/repos/owner/name/labels?limit=50&page=2>; rel="last" """));
        handler.Enqueue(LinkedPages.Json(
            """[{ "id": 8, "name": "area: api", "color": "#0E8A16", "description": "" }, { "id": 9, "name": "bare", "color": "ffffff" }]""",
            """<https://forge.example/api/v1/repos/owner/name/labels?limit=50&page=1>; rel="first",<https://forge.example/api/v1/repos/owner/name/labels?limit=50&page=1>; rel="prev" """));
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        var labels = await store.List(TestContext.Current.CancellationToken);

        // Assert
        labels.ShouldBe(
        [
            new RepositoryLabel("ci", "1d76db", "Continuous integration"),
            new RepositoryLabel("area: api", "0E8A16", string.Empty),
            new RepositoryLabel("bare", "ffffff", string.Empty),
        ]);
        handler.Requests.Select(static request => request.PathAndQuery).ShouldBe(
        [
            LabelsPath + "?limit=50&page=1",
            LabelsPath + "?limit=50&page=2",
        ]);
        handler.Requests.ShouldAllSatisfy(static request => request.Method.ShouldBe(HttpMethod.Get));
        handler.Requests[0].Message.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("token", "secret"));
        handler.Requests[0].Message.RequestUri!.Host.ShouldBe("forge.example");
    }

    [Fact]
    public async Task Listing_without_a_link_header_reads_pages_until_the_total_count()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(LinkedPages.Counted("""[{ "id": 1, "name": "a", "color": "000000", "description": "" }, { "id": 2, "name": "b", "color": "111111", "description": "" }]""", 3));
        handler.Enqueue(LinkedPages.Counted("""[{ "id": 3, "name": "c", "color": "222222", "description": "" }]""", 3));
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        var labels = await store.List(TestContext.Current.CancellationToken);

        // Assert
        labels.Select(static label => label.Name).ShouldBe(["a", "b", "c"]);
        handler.Requests.Select(static request => request.PathAndQuery).ShouldBe(
        [
            LabelsPath + "?limit=50&page=1",
            LabelsPath + "?limit=50&page=2",
        ]);
    }

    [Fact]
    public async Task Listing_without_paging_headers_stops_at_a_short_page()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """[{ "id": 1, "name": "a", "color": "000000", "description": "" }]""");
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        var labels = await store.List(TestContext.Current.CancellationToken);

        // Assert
        labels.ShouldHaveSingleItem().Name.ShouldBe("a");
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Listing_a_public_repository_sends_no_credentials_and_stops_at_an_empty_page()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(LinkedPages.Json("[]", """<https://forge.example/api/v1/repos/owner/name/labels?limit=50&page=2>; rel="next" """));
        using var store = new ForgejoLabelStore(handler, new Uri("https://forge.example/forgejo/"), "owner/name", UserAgent, null);

        // Act
        var labels = await store.List(TestContext.Current.CancellationToken);

        // Assert
        labels.ShouldBeEmpty();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.PathAndQuery.ShouldBe("/forgejo" + LabelsPath + "?limit=50&page=1");
        request.Message.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task A_label_list_longer_than_one_hundred_pages_fails_instead_of_being_cut()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var page = 0; page < 100; page++)
        {
            handler.Enqueue(LinkedPages.Counted(LinkedPages.Labels((page * 50) + 1, 50), 10_000));
        }

        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null);
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Act
        var exception = await list.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("The labels of owner/name span more than 100 pages of 50 items; nothing was read from a partial list.");
        handler.Requests.Count.ShouldBe(100);
    }

    [Fact]
    public async Task Creating_posts_the_name_the_color_with_a_hash_and_the_description()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, """{ "id": 10, "name": "ci", "color": "1d76db", "description": "Continuous integration" }""");
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        await store.Create(new RepositoryLabel("ci", "1d76db", "Continuous integration"), TestContext.Current.CancellationToken);

        // Assert
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe(LabelsPath);
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetRawText().ShouldBe("""{"name":"ci","color":"#1d76db","description":"Continuous integration"}""");
    }

    [Fact]
    public async Task Updating_patches_the_label_by_the_ID_that_the_listing_read_for_its_name()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(LinkedPages.Counted("""[{ "id": 42, "name": "Type: Epic", "color": "000000", "description": "Parent issue" }]""", 1));
        handler.EnqueueJson(HttpStatusCode.OK, """{ "id": 42, "name": "type: epic", "color": "6f42c1", "description": "Parent issue" }""");
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");
        _ = await store.List(TestContext.Current.CancellationToken);

        // Act
        await store.Update("type: epic", new RepositoryLabel("type: epic", "6f42c1", "Parent issue"), TestContext.Current.CancellationToken);

        // Assert
        var request = handler.Requests[1];
        request.Method.ShouldBe(HttpMethod.Patch);
        request.PathAndQuery.ShouldBe(LabelsPath + "/42");
        using var body = JsonDocument.Parse(request.Body!);
        body.RootElement.GetRawText().ShouldBe("""{"name":"type: epic","color":"#6f42c1","description":"Parent issue"}""");
    }

    [Fact]
    public async Task Updating_a_label_the_listing_did_not_read_sends_no_request()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        Func<Task> update = () => store.Update("ci", new RepositoryLabel("ci", "1d76db", string.Empty), TestContext.Current.CancellationToken);

        // Assert
        var exception = await update.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("Forgejo label sync has not read label ci; list the labels before updating one.");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_refused_write_names_the_status_and_the_label_but_not_the_response()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.UnprocessableEntity, """{ "message": "secret-echo" }""");
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        Func<Task> create = () => store.Create(new RepositoryLabel("ci", "1d76db", string.Empty), TestContext.Current.CancellationToken);

        // Assert
        var exception = await create.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("Forgejo label sync failed with HTTP 422 for label ci. Rerun the sync after fixing the cause.");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    public async Task A_refused_or_redirected_listing_fails_with_the_status(HttpStatusCode status)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(status, "{}");
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Assert
        var exception = await list.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe($"Forgejo label sync failed with HTTP {(int)status}.");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""[{ "name": "ci", "color": "1d76db" }]""")]
    [InlineData("""[{ "id": 0, "name": "ci", "color": "1d76db" }]""")]
    [InlineData("""[{ "id": 1, "name": null, "color": "1d76db" }]""")]
    [InlineData("""[{ "id": 1, "name": "ci", "color": 5 }]""")]
    [InlineData("""[{ "id": 1, "name": "ci", "color": "1d76db", "description": 3 }]""")]
    [InlineData("""[{ "id": 1, "id": 2, "name": "ci", "color": "1d76db" }]""")]
    [InlineData("not json")]
    public async Task An_invalid_listing_is_rejected(string json)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, json);
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null);

        // Act
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Assert
        var exception = await list.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("Forgejo label sync returned an invalid label response.");
    }

    [Fact]
    public async Task A_transport_failure_is_reported_without_its_message()
    {
        // Arrange
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("https://user:secret@forge.example"));
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, "secret");

        // Act
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Assert
        var exception = await list.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("Forgejo label sync request failed.");
    }

    [Fact]
    public async Task A_request_that_takes_longer_than_thirty_seconds_times_out()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        using var handler = new ClockAdvancingHandler(timeProvider);
        using var store = new ForgejoLabelStore(handler, Server, "owner/name", UserAgent, null, timeProvider);

        // Act
        Func<Task> list = () => store.List(TestContext.Current.CancellationToken);

        // Assert
        var exception = await list.ShouldThrow<TimeoutException>();
        exception.Message.ShouldBe("Forgejo label sync did not answer within 30 seconds.");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("owner/name/extra")]
    [InlineData("owner/..")]
    [InlineData("owner/name?x=1")]
    [InlineData("/name")]
    public void A_repository_that_is_not_owner_slash_name_is_rejected(string repository)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        Action create = () => new ForgejoLabelStore(handler, Server, repository, UserAgent, null).Dispose();

        // Assert
        create.ShouldThrow<InvalidOperationException>().Message.ShouldBe("--repository must name a Forgejo repository as owner/name.");
    }

    [Fact]
    public void A_server_that_is_not_https_is_rejected()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        Action create = () => new ForgejoLabelStore(handler, new Uri("http://forge.example"), "owner/name", UserAgent, null).Dispose();

        // Assert
        create.ShouldThrow<ArgumentException>();
    }
}
