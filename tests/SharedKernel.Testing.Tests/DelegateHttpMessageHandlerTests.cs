using PandaBear.Testing.Http;
using System.Net;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the delegate-backed HTTP message handler and the tracking stream content.</summary>
public sealed class DelegateHttpMessageHandlerTests
{
    [Fact]
    public static async Task Delegate_handler_returns_the_synchronous_factory_response()
    {
        // Arrange
        using var handler = new DelegateHttpMessageHandler(static _ => new HttpResponseMessage(HttpStatusCode.Accepted));
        using var client = new HttpClient(handler);

        // Act
        using var response = await client.GetAsync(new Uri("https://example.test/"), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public static async Task Delegate_handler_does_not_call_the_factory_when_already_canceled()
    {
        // Arrange
        var calls = 0;
        using var handler = new DelegateHttpMessageHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> action = () => client.GetAsync(new Uri("https://example.test/"), cancellation.Token);

        // Assert
        await action.ShouldThrowAssignableTo<OperationCanceledException>();
        calls.ShouldBe(0);
    }

    [Fact]
    public static async Task Tracking_stream_content_counts_serializations_and_disposes_the_source()
    {
        // Arrange
        using var source = new MemoryStream([1, 2, 3]);
        var content = new TrackingStreamContent(source);

        // Act
        using var destination = new MemoryStream();
        await content.CopyToAsync(destination, TestContext.Current.CancellationToken);
        content.Dispose();

        // Assert
        content.SerializationCount.ShouldBe(1);
        destination.ToArray().ShouldBe([1, 2, 3]);
        source.CanRead.ShouldBeFalse();
    }
}
