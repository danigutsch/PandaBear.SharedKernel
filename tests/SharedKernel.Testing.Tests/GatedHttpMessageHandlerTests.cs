using SharedKernel.Testing.Http;
using System.Net;
using System.Net.Http.Headers;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the gated HTTP message handler.</summary>
public sealed class GatedHttpMessageHandlerTests
{
    private static readonly Uri BaseAddress = new("https://example.test/");

    [Fact]
    public static async Task Gated_handler_records_an_arriving_request_and_holds_it_until_released()
    {
        // Arrange
        using var handler = new GatedHttpMessageHandler();
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");

        // Act
        var send = client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);
        var arrived = await handler.WaitForRequests(1, TestContext.Current.CancellationToken);
        var completedWhileClosed = send.IsCompleted;
        var pendingWhileClosed = handler.PendingCount;
        handler.Release();
        using var response = await send;

        // Assert
        completedWhileClosed.ShouldBeFalse();
        pendingWhileClosed.ShouldBe(1);
        arrived.ShouldHaveSingleItem().Message.Headers.Authorization.ShouldNotBeNull().ToString().ShouldBe("Bearer token");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.PendingCount.ShouldBe(0);
    }

    [Fact]
    public static async Task Gated_handler_lets_later_requests_through_once_released()
    {
        // Arrange
        using var handler = new GatedHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.Release();

        // Act
        using var first = await client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public static async Task Gated_handler_cancels_a_held_request_without_reaching_the_inner_handler()
    {
        // Arrange
        using var inner = new RecordingHttpMessageHandler();
        using var handler = new GatedHttpMessageHandler(inner);
        using var client = new HttpClient(handler, disposeHandler: false);
        using var cancellation = new CancellationTokenSource();
        var send = client.GetAsync(BaseAddress, cancellation.Token);
        await handler.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Act
        await cancellation.CancelAsync();
        Func<Task> act = () => send;

        // Assert
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
        handler.PendingCount.ShouldBe(0);
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Gated_handler_forwards_released_requests_to_the_inner_handler()
    {
        // Arrange
        using var inner = new RoutingHttpMessageHandler();
        inner.Map(HttpMethod.Get, "/ready", HttpStatusCode.NoContent);
        using var handler = new GatedHttpMessageHandler(inner);
        using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = BaseAddress };
        var send = client.GetAsync(new Uri("/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        await handler.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Act
        handler.Release();
        using var response = await send;

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        inner.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public static async Task Gated_handler_fails_held_requests_when_disposed()
    {
        // Arrange
        var handler = new GatedHttpMessageHandler();
        using var client = new HttpClient(handler, disposeHandler: false);
        var send = client.GetAsync(BaseAddress, TestContext.Current.CancellationToken);
        await handler.WaitForRequests(1, TestContext.Current.CancellationToken);

        // Act
        handler.Dispose();
        Func<Task> act = () => send;

        // Assert
        await act.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public static void Gated_handler_releases_synchronous_sends()
    {
        // Arrange
        using var inner = new RecordingHttpMessageHandler(HttpStatusCode.Accepted);
        using var handler = new GatedHttpMessageHandler(inner);
        using var client = new HttpClient(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseAddress);
        handler.Release();

        // Act
        using var response = client.Send(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        handler.Requests.ShouldHaveSingleItem();
    }
}
