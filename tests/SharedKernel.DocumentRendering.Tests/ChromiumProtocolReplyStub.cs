using System.Text.Json;
using Microsoft.Playwright;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Returns fixed synthetic JSON replies to the PDF pipe reader.</summary>
internal sealed class ChromiumProtocolReplyStub(params string[] replies) : ICDPSession
{
    private int _next;

    /// <summary>Gets the parameters of the print command.</summary>
    internal IReadOnlyDictionary<string, object>? PrintOptions { get; private set; }

    /// <inheritdoc />
    public event EventHandler<ICDPSession> Close
    {
        add
        {
        }
        remove
        {
        }
    }

    /// <inheritdoc />
    public Task DetachAsync() => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ICDPSessionEvent Event(string eventName) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<JsonElement?> SendAsync(
        string method, Dictionary<string, object>? args = null, CDPSessionSendOptions? options = null)
    {
        if (method == "Page.printToPDF")
        {
            PrintOptions = args;
        }

        using var reply = JsonDocument.Parse(replies[_next++]);
        return Task.FromResult<JsonElement?>(reply.RootElement.Clone());
    }
}
