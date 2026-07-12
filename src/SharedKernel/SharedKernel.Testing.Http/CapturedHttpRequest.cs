namespace SharedKernel.Testing.Http;

/// <summary>
/// Describes an HTTP request captured by a test message handler.
/// </summary>
public sealed class CapturedHttpRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedHttpRequest" /> class.
    /// </summary>
    /// <param name="message">The captured request message.</param>
    /// <param name="pathAndQuery">The captured request path and query.</param>
    /// <param name="body">The captured request body.</param>
    public CapturedHttpRequest(HttpRequestMessage message, string pathAndQuery, string? body)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(pathAndQuery);

        Message = message;
        PathAndQuery = pathAndQuery;
        Body = body;
    }

    /// <summary>
    /// Gets the captured request message.
    /// </summary>
    public HttpRequestMessage Message { get; }

    /// <summary>
    /// Gets the captured request path and query.
    /// </summary>
    public string PathAndQuery { get; }

    /// <summary>
    /// Gets the captured request body.
    /// </summary>
    public string? Body { get; }

    /// <summary>
    /// Gets the captured request method.
    /// </summary>
    public HttpMethod Method => Message.Method;
}
