using System.Net;

namespace SharedKernel.HttpClients.Tests;

/// <summary>Checks ownership transfer for an upstream streaming HTTP response.</summary>
public sealed class HttpResponseBodyTests
{
    [Fact]
    public async Task A_missing_body_disposes_the_response_without_invoking_the_owner_factory()
    {
        // Arrange
        var content = new TrackingStringContent("missing", "text/plain");
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = content };

        // Act
        var body = await HttpResponseBody.ReadOrNotFound<Stream>(response,
            (_, _, _) => throw new InvalidOperationException("Missing content cannot be transferred."),
            TestContext.Current.CancellationToken);

        // Assert
        body.ShouldBeNull();
        content.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_successful_response_transfers_the_open_stream_and_the_media_type()
    {
        // Arrange
        var content = new TrackingStringContent("fictional poster", "text/plain");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        var ct = TestContext.Current.CancellationToken;
        HttpResponseMessage? owner = null;
        string? type = null;

        // Act
        using var body = await HttpResponseBody.ReadOrNotFound(response, (message, stream, mediaType) =>
        {
            owner = message;
            type = mediaType;
            return stream;
        }, ct);

        // Assert
        owner.ShouldBe(response);
        type.ShouldBe("text/plain");
        body.ShouldNotBeNull().CanRead.ShouldBeTrue();
        content.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unsuccessful_status_disposes_the_response()
    {
        // Arrange
        var content = new TrackingStringContent("unavailable", "text/plain");
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content };

        // Act
        Func<Task> read = async () => await HttpResponseBody.ReadOrNotFound(response,
            (_, stream, _) => stream, TestContext.Current.CancellationToken);

        // Assert
        await read.ShouldThrow<HttpRequestException>();
        content.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_failed_stream_open_disposes_the_response()
    {
        // Arrange
        var content = new ContractHttpValidationNotSupportedContent();
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        // Act
        Func<Task> read = async () => await HttpResponseBody.ReadOrNotFound(response,
            (_, stream, _) => stream, TestContext.Current.CancellationToken);

        // Assert
        await read.ShouldThrow<NotSupportedException>();
        content.WasRead.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_failed_owner_factory_disposes_the_response()
    {
        // Arrange
        var content = new TrackingStringContent("fictional poster", "text/plain");
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        // Act
        Func<Task> read = async () => await HttpResponseBody.ReadOrNotFound<Stream>(response,
            (_, _, _) => throw new InvalidOperationException("Synthetic owner failure."),
            TestContext.Current.CancellationToken);

        // Assert
        await read.ShouldThrow<InvalidOperationException>();
        content.IsDisposed.ShouldBeTrue();
    }
}
