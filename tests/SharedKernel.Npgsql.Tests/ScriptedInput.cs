namespace PandaBear.Npgsql.Tests;

/// <summary>
/// A readable stream that delivers the chunks it was given and then blocks every read until the test ends it, like the
/// read end of a pipe whose writer stays open. Disposing it ends it, so a reader thread never outlives its test.
/// </summary>
/// <param name="chunks">What the reader receives, one chunk per read, before the reader has to wait.</param>
internal sealed class ScriptedInput(params byte[][] chunks) : Stream
{
    private readonly Queue<byte[]> _chunks = new(chunks);
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes when the reader has consumed every chunk and waits for more.</summary>
    public Task Waiting => _waiting.Task;

    /// <summary>Lets the blocked reader see the end of the input.</summary>
    public void End() => _ended.TrySetResult();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_chunks.TryDequeue(out var chunk))
        {
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        _waiting.TrySetResult();
        _ended.Task.Wait();
        return 0;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            End();
        }

        base.Dispose(disposing);
    }
}
