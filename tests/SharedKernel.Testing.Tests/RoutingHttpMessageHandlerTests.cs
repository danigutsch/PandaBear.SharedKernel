using PandaBear.Testing.Http;
using System.Net;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the method-and-path routing HTTP message handler.</summary>
public sealed class RoutingHttpMessageHandlerTests
{
    private static readonly Uri BaseAddress = new("https://example.test/");

    [Fact]
    public static async Task Routing_handler_answers_by_method_and_path_ignoring_the_query()
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler();
        handler.MapJson(HttpMethod.Get, "/api/items", "[1]");
        handler.Map(HttpMethod.Post, "/api/items", HttpStatusCode.Created);
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };
        using var content = new StringContent("{}");

        // Act
        using var read = await client.GetAsync(new Uri("/api/items?page=2", UriKind.Relative), TestContext.Current.CancellationToken);
        using var created = await client.PostAsync(new Uri("/api/items", UriKind.Relative), content, TestContext.Current.CancellationToken);

        // Assert
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/json");
        var body = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("[1]");
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        handler.Requests.Select(static request => request.PathAndQuery).ShouldBe(["/api/items?page=2", "/api/items"]);
        handler.LastRequest.ShouldNotBeNull().Body.ShouldBe("{}");
    }

    [Fact]
    public static async Task Routing_handler_answers_not_found_for_an_unmapped_path_or_method_and_records_it()
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler();
        handler.Map(HttpMethod.Get, "/known", HttpStatusCode.OK);
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };

        // Act
        using var unknownPath = await client.GetAsync(new Uri("/unknown", UriKind.Relative), TestContext.Current.CancellationToken);
        using var otherMethod = await client.DeleteAsync(new Uri("/known", UriKind.Relative), TestContext.Current.CancellationToken);
        using var otherCase = await client.GetAsync(new Uri("/KNOWN", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        unknownPath.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        otherMethod.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        otherCase.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public static async Task Routing_handler_uses_the_supplied_fallback_for_unmapped_requests()
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler(static request =>
            new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.Gone : HttpStatusCode.Conflict));
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };

        // Act
        using var response = await client.GetAsync(new Uri("/anything", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public static async Task Routing_handler_replaces_a_route_mapped_again()
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler();
        handler.Map(HttpMethod.Get, "/status", HttpStatusCode.OK);
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };
        using var before = await client.GetAsync(new Uri("/status", UriKind.Relative), TestContext.Current.CancellationToken);

        // Act
        handler.Map(HttpMethod.Get, "/status", static _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var after = await client.GetAsync(new Uri("/status", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        after.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public static async Task Routing_handler_creates_a_new_response_for_every_matching_request()
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler();
        handler.MapJson(HttpMethod.Get, "/data", """{"a":1}""");
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };

        // Act
        var first = await client.GetStringAsync(new Uri("/data", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetStringAsync(new Uri("/data", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        first.ShouldBe("""{"a":1}""");
        second.ShouldBe("""{"a":1}""");
    }

    [Theory]
    [InlineData("")]
    [InlineData("api/items")]
    [InlineData("/api/items?x=1")]
    public static void Routing_handler_rejects_a_path_that_is_not_an_absolute_path(string path)
    {
        // Arrange
        using var handler = new RoutingHttpMessageHandler();
        Action act = () => handler.Map(HttpMethod.Get, path, HttpStatusCode.OK);

        // Act, Assert
        act.ShouldThrow<ArgumentException>();
    }
}
