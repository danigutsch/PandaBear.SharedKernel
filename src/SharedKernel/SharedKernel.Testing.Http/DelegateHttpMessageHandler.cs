namespace PandaBear.Testing.Http;

/// <summary>An <see cref="HttpMessageHandler" /> that answers every request with a caller-supplied delegate.</summary>
public sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    /// <summary>Initializes a new instance of the <see cref="DelegateHttpMessageHandler" /> class.</summary>
    /// <param name="respond">Creates the response for each request.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="respond"/> is <see langword="null"/>.</exception>
    public DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        _respond = (request, _) => Task.FromResult(respond(request));
    }

    /// <summary>Initializes a new instance of the <see cref="DelegateHttpMessageHandler" /> class.</summary>
    /// <param name="respond">Creates the response for each request and may observe cancellation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="respond"/> is <see langword="null"/>.</exception>
    public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        _respond = respond;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _respond(request, cancellationToken);
    }
}
