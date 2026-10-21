namespace PandaBear.IO.Tests;

internal sealed class FailingReadStream(CancellationTokenSource? cancellation) : MemoryStream(new byte[200000])
{
    private bool first = true;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (first)
        {
            first = false;
            return await base.ReadAsync(buffer, cancellationToken);
        }

        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        throw new IOException("Read failed after spooling began.");
    }
}
