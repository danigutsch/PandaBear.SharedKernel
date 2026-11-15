using System.Collections.Concurrent;
using System.Net;

namespace PandaBear.LoadTesting.Tests;

/// <summary>An HTTP handler that answers each request with the next status of a script, repeating the last one.</summary>
/// <param name="statuses">The status of each answer, in order.</param>
internal sealed class ScriptedHttpHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
{
    private int _count;

    /// <summary>Gets each requested URI, in order.</summary>
    public ConcurrentQueue<Uri> Requests { get; } = new();

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request.RequestUri!);
        var index = Math.Min(Interlocked.Increment(ref _count), statuses.Length) - 1;
        return Task.FromResult(new HttpResponseMessage(statuses[index]));
    }
}
