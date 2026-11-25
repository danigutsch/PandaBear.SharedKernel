namespace SharedKernel.Testing.Tests;

/// <summary>Tests the stream wrapper that records synchronous and asynchronous disposal.</summary>
public sealed class DisposalTrackingStreamTests
{
    private static readonly TimeSpan FailureGuard = TimeSpan.FromSeconds(10);

    [Fact]
    public static async Task Disposal_tracking_stream_delegates_reads_and_writes_to_the_inner_stream()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new MemoryStream();
        using var stream = new DisposalTrackingStream(inner);

        // Act
        await stream.WriteAsync(new byte[] { 1, 2, 3 }, ct);
        stream.WriteByte(4);
        stream.Position = 0;
        var buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer, ct);

        // Assert
        buffer.ShouldBe(new byte[] { 1, 2, 3, 4 });
        stream.Length.ShouldBe(4);
        stream.CanSeek.ShouldBeTrue();
        stream.InnerStream.ShouldBeSameAs(inner);
    }

    [Fact]
    public static void Disposal_tracking_stream_records_synchronous_disposal_and_disposes_the_inner_stream()
    {
        // Arrange
        var inner = new TrackingReadStream([1]);
        var stream = new DisposalTrackingStream(inner);

        // Act
        stream.Dispose();
        stream.Close();

        // Assert
        stream.Disposed.IsCompletedSuccessfully.ShouldBeTrue();
        stream.DisposeCount.ShouldBe(2);
        stream.AsyncDisposeCount.ShouldBe(0);
        stream.CanRead.ShouldBeFalse();
        inner.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public static async Task Disposal_tracking_stream_records_asynchronous_disposal()
    {
        // Arrange
        var inner = new DisposalTrackingStream(new MemoryStream());
        var stream = new DisposalTrackingStream(inner);

        // Act
        await stream.DisposeAsync();

        // Assert
        stream.Disposed.IsCompletedSuccessfully.ShouldBeTrue();
        stream.AsyncDisposeCount.ShouldBe(1);
        stream.DisposeCount.ShouldBe(0);
        inner.AsyncDisposeCount.ShouldBe(1);
    }

    [Fact]
    public static async Task Disposal_tracking_stream_completes_disposed_even_when_the_inner_stream_fails_to_dispose()
    {
        // Arrange
        await using var stream = new DisposalTrackingStream(new FailingDisposeStream());
        Func<Task> dispose = async () => await stream.DisposeAsync();

        // Act
        await dispose.ShouldThrow<IOException>();

        // Assert
        stream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public static async Task Disposal_tracking_stream_reports_a_blocked_inner_read_cancelled_by_the_reader()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var inner = new BlockingReadStream("i"u8.ToArray(), []);
        await using var stream = new DisposalTrackingStream(inner);
        using var cancellation = new CancellationTokenSource();
        _ = await stream.ReadAsync(new byte[4], ct);
        var read = stream.ReadAsync(new byte[4], cancellation.Token).AsTask();
        await inner.Blocked.WaitAsync(FailureGuard, ct);

        // Act
        await cancellation.CancelAsync();
        Func<Task> pending = () => read.WaitAsync(FailureGuard, ct);

        // Assert
        await pending.ShouldThrowAssignableTo<OperationCanceledException>();
        inner.BlockedReadCancelled.ShouldBeTrue();
    }

    [Fact]
    public static void Disposal_tracking_stream_rejects_a_null_inner_stream()
    {
        // Arrange
        Action create = () => _ = new DisposalTrackingStream(null!);

        // Act, Assert
        create.ShouldThrow<ArgumentNullException>();
    }
}
