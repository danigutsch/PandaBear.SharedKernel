using PandaBear.Testing.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the recording HTTP message handler.</summary>
public sealed class RecordingHttpMessageHandlerTests
{
    private static readonly Uri BaseAddress = new("https://example.test/");

    [Fact]
    public static async Task Recording_handler_answers_every_request_with_ok_by_default()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };

        // Act
        using var first = await client.GetAsync(new Uri("/a", UriKind.Relative), TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(new Uri("/b?x=1", UriKind.Relative), TestContext.Current.CancellationToken);
        using var third = await client.DeleteAsync(new Uri("/c", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        third.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.Requests.Select(static request => request.PathAndQuery).ShouldBe(["/a", "/b?x=1", "/c"]);
        handler.LastRequest.ShouldNotBeNull().Method.ShouldBe(HttpMethod.Delete);
    }

    [Fact]
    public static async Task Recording_handler_answers_with_the_configured_status()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);

        // Act
        using var response = await client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public static async Task Recording_handler_keeps_the_body_and_headers_after_the_request_is_disposed()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

        // Act
        using (var request = new HttpRequestMessage(HttpMethod.Post, BaseAddress))
        {
            request.Content = new StringContent("grant_type=x", Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
            using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        }

        // Assert
        var recorded = handler.Requests.ShouldHaveSingleItem();
        recorded.Body.ShouldBe("grant_type=x");
        recorded.Message.Headers.Authorization.ShouldNotBeNull().ToString().ShouldBe("Bearer token");
    }

    [Fact]
    public static async Task Recording_handler_passes_the_buffered_request_to_the_response_delegate()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler(static request =>
            HttpResponseFactory.Json($$"""{"echo":"{{request.Body}}"}"""));
        using var client = new HttpClient(handler);
        using var content = new StreamContent(new MemoryStream("payload"u8.ToArray()));

        // Act
        using var response = await client.PostAsync(BaseAddress, content, TestContext.Current.CancellationToken);

        // Assert
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"echo":"payload"}""");
        handler.LastRequest.ShouldNotBeNull().Body.ShouldBe("payload");
    }

    [Fact]
    public static async Task Recording_handler_records_every_concurrent_request()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler);

        // Act
        await Parallel.ForAsync(0, 64, TestContext.Current.CancellationToken, async (index, token) =>
        {
            using var response = await client.GetAsync(new Uri(BaseAddress, "/" + index), token);
        });

        // Assert
        handler.Requests.Count.ShouldBe(64);
        handler.Requests.Select(static request => request.PathAndQuery).Distinct().Count().ShouldBe(64);
    }

    [Fact]
    public static async Task Recording_handler_completes_a_wait_once_the_requested_count_arrives()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var wait = handler.WaitForRequests(2, TestContext.Current.CancellationToken);

        // Act
        using var first = await client.GetAsync(new Uri(BaseAddress, "/1"), TestContext.Current.CancellationToken);
        var completedAfterOne = wait.IsCompleted;
        using var second = await client.GetAsync(new Uri(BaseAddress, "/2"), TestContext.Current.CancellationToken);
        var requests = await wait;

        // Assert
        completedAfterOne.ShouldBeFalse();
        requests.Select(static request => request.PathAndQuery).ShouldBe(["/1", "/2"]);
    }

    [Fact]
    public static async Task Recording_handler_wait_honours_cancellation()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        using var cancellation = new CancellationTokenSource();
        var wait = handler.WaitForRequests(1, cancellation.Token);

        // Act
        await cancellation.CancelAsync();
        Func<Task> act = () => wait;

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public static void Recording_handler_rejects_a_wait_for_fewer_than_one_request()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler();
        Action act = () => _ = handler.WaitForRequests(0, TestContext.Current.CancellationToken);

        // Act, Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public static void Recording_handler_supports_synchronous_sends()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, BaseAddress) { Content = new StringContent("sync") };

        // Act
        using var response = client.Send(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        handler.LastRequest.ShouldNotBeNull().Body.ShouldBe("sync");
    }

    [Fact]
    public static async Task Recording_handler_rejects_a_delegate_that_returns_no_response()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler(static _ => null!);
        using var client = new HttpClient(handler);
        Func<Task> act = () => client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);

        // Act, Assert
        await act.ShouldThrow<InvalidOperationException>();
    }
}
