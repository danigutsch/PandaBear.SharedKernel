using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Answers HTTP requests from queued responses per address, without a network. The last response for an address repeats,
/// and an address without responses returns 404.
/// </summary>
internal sealed class FakeReleaseServer : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<CancellationToken, Task<HttpResponseMessage>>>> _responses = new(StringComparer.Ordinal);

    /// <summary>Gets every request that reached the server, in arrival order.</summary>
    public List<FakeReleaseRequest> Requests { get; } = [];

    /// <summary>Gets the absolute address of every request in <see cref="Requests"/>, in arrival order.</summary>
    public string[] Addresses => [.. Requests.Select(static request => request.Uri.AbsoluteUri)];

    /// <summary>Gets the release address of an asset of <see cref="CandidateStagingScope.Version"/>.</summary>
    public static string ReleaseUrl(string asset) =>
        "https://github.com/" + CandidateStagingScope.Repository + "/releases/download/v" + CandidateStagingScope.Version + "/" + asset;

    /// <summary>Gets the signed asset-host address that the release address of an asset redirects to.</summary>
    public static string AssetUrl(string asset) =>
        "https://release-assets.githubusercontent.com/github-production-release-asset/1/" + asset + "?sp=r&sig=secret";

    /// <summary>
    /// Serves <paramref name="manifest"/>, or the scope's pin, as the release candidate.json and redirects each package to
    /// the asset host, as GitHub does.
    /// </summary>
    public static FakeReleaseServer For(CandidateStagingScope scope, byte[]? manifest = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var server = new FakeReleaseServer();
        var candidate = manifest ?? scope.Pin;
        server.Add(ReleaseUrl("candidate.json"), () => Ok(candidate));
        foreach (var (name, content) in scope.Packages)
        {
            server.Add(ReleaseUrl(name), () => Redirect(AssetUrl(name)));
            server.Add(AssetUrl(name), () => Ok(content));
        }

        return server;
    }

    /// <summary>Returns a 200 response whose body is <paramref name="content"/>.</summary>
    public static HttpResponseMessage Ok(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    /// <summary>Returns a 302 response whose <c>Location</c> header is <paramref name="location"/>.</summary>
    public static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }

    /// <summary>Returns an empty response with the given status code.</summary>
    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    /// <summary>Returns an empty response with the given status code and a <c>Retry-After</c> header of <paramref name="delay"/>.</summary>
    public static HttpResponseMessage RetryAfter(HttpStatusCode status, TimeSpan delay)
    {
        var response = Status(status);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
        return response;
    }

    /// <summary>Replaces the responses for <paramref name="uri"/>.</summary>
    public void Set(string uri, params Func<HttpResponseMessage>[] responses)
    {
        _responses.Remove(uri);
        foreach (var response in responses)
        {
            Add(uri, response);
        }
    }

    /// <summary>Queues a synchronous response factory for <paramref name="uri"/>.</summary>
    public void Add(string uri, Func<HttpResponseMessage> response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Add(uri, _ => Task.FromResult(response()));
    }

    /// <summary>Queues an asynchronous response factory for <paramref name="uri"/>, so a test can delay or cancel the answer.</summary>
    public void Add(string uri, Func<CancellationToken, Task<HttpResponseMessage>> response)
    {
        if (!_responses.TryGetValue(uri, out var queue))
        {
            queue = new Queue<Func<CancellationToken, Task<HttpResponseMessage>>>();
            _responses[uri] = queue;
        }

        queue.Enqueue(response);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri!;
        Requests.Add(new FakeReleaseRequest(uri, request.Headers.UserAgent.ToString(), request.Headers.Authorization is not null));
        if (!_responses.TryGetValue(uri.AbsoluteUri, out var queue))
        {
            return Status(HttpStatusCode.NotFound);
        }

        var respond = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return await respond(cancellationToken);
    }
}
