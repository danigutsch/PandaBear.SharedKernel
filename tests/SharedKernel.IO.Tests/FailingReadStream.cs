namespace PandaBear.IO.Tests;

/// <summary>
/// A 200,000-byte in-memory stream whose first read succeeds and later reads fail, used to test spooling errors.
/// </summary>
/// /// <param name="cancellation">When set, later reads cancel this source and throw OperationCanceledException instead of IOException.</param>
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
