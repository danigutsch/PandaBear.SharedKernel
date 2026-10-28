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
    public static async Task Delegate_handler_passes_the_callers_token_to_the_asynchronous_factory()
    {
        // Arrange
        CancellationToken observed = default;
        using var handler = new DelegateHttpMessageHandler((_, token) =>
        {
            observed = token;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://example.test/"));
        using var cancellation = new CancellationTokenSource();

        // Act
        using var response = await invoker.SendAsync(request, cancellation.Token);

        // Assert
        observed.ShouldBe(cancellation.Token);
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
