using System.Net;
using PandaBear.Testing;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>An in-process loopback HTTP endpoint that records requests and answers with scripted S3 status codes.</summary>
internal sealed class LoopbackS3Stub : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Queue<HttpStatusCode> scriptedStatuses;
    private readonly Lock gate = new();
    private readonly List<RecordedPutRequest> requests = [];
    private readonly Task loop;

    /// <summary>Starts listening on a free loopback port.</summary>
    /// <param name="statuses">Statuses for successive requests; requests past the script get 200.</param>
    public LoopbackS3Stub(params HttpStatusCode[] statuses)
    {
        scriptedStatuses = new Queue<HttpStatusCode>(statuses);
        var port = LoopbackPort.GetFree();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        listener.Prefixes.Add(BaseUri.AbsoluteUri);
        listener.Start();
        loop = Task.Run(Serve);
    }

    /// <summary>Gets the endpoint base URI.</summary>
    public Uri BaseUri
    {
        get;
    }

    /// <summary>Gets a snapshot of the requests received so far.</summary>
    public IReadOnlyList<RecordedPutRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        listener.Close();
        try
        {
            loop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
        }
    }

    private async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            using var body = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(body);
            var headers = context.Request.Headers.AllKeys
                .Where(static key => key is not null)
                .ToDictionary(static key => key!, key => context.Request.Headers[key] ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            HttpStatusCode status;
            lock (gate)
            {
                requests.Add(new RecordedPutRequest(context.Request.HttpMethod, context.Request.RawUrl ?? string.Empty, context.Request.ContentLength64, headers, body.ToArray()));
                status = scriptedStatuses.Count > 0 ? scriptedStatuses.Dequeue() : HttpStatusCode.OK;
            }

            context.Response.StatusCode = (int)status;
            context.Response.Headers["ETag"] = "\"stub\"";
            if (status != HttpStatusCode.OK)
            {
                var error = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>InternalError</Code><Message>scripted</Message></Error>"u8.ToArray();
                context.Response.ContentType = "application/xml";
                await context.Response.OutputStream.WriteAsync(error);
            }

            context.Response.Close();
        }
    }
}
