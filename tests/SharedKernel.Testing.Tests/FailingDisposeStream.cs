namespace PandaBear.Testing.Tests;

/// <summary>Empty stream whose asynchronous disposal fails, to prove a wrapper still records its own disposal.</summary>
internal sealed class FailingDisposeStream : MemoryStream
{
    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        throw new IOException("dispose failure");
    }
}
