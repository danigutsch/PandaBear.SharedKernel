namespace PandaBear.Testing.Tests;

/// <summary>Checks the read observations and ownership of the borrowed stream.</summary>
public sealed class ReadObservingStreamTests
{
    [Fact]
    public static void Requires_a_read_operation_and_an_observer()
    {
        // Arrange
        using var inner = new TrackingReadStream([]);

        // Act
        Action missingRead = () => _ = new ReadObservingStream(null!, (_, _) => { });
        Action missingObserver = () => _ = new ReadObservingStream(inner.ReadAsync, null!);

        // Assert
        missingRead.ShouldThrow<ArgumentNullException>();
        missingObserver.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public static async Task Observes_short_array_and_memory_reads_and_end_of_stream()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new TrackingReadStream([1, 2, 3]) { MaxBytesPerRead = 2 };
        var observations = new List<(int Requested, int Returned)>();
        using var stream = new ReadObservingStream(inner.ReadAsync, (requested, returned) => observations.Add((requested, returned)));
        var buffer = new byte[5];
        Func<byte[], int, int, CancellationToken, Task<int>> readArray = stream.ReadAsync;

        // Act
        var first = await stream.ReadAsync(buffer.AsMemory(0, 4), ct);
        var second = await readArray(buffer, 3, 2, ct);
        var end = await stream.ReadAsync(buffer.AsMemory(), ct);

        // Assert
        first.ShouldBe(2);
        second.ShouldBe(1);
        end.ShouldBe(0);
        buffer.ShouldBe(new byte[] { 1, 2, 0, 3, 0 });
        observations.ShouldBe([(4, 2), (2, 1), (5, 0)]);
    }

    [Fact]
    public static async Task Propagates_cancellation_of_a_blocked_read_without_observing_bytes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(10);
        using var inner = new BlockingReadStream();
        using var cancellation = new CancellationTokenSource();
        var observations = 0;
        using var stream = new ReadObservingStream(inner.ReadAsync, (_, _) => observations++);

        // Act
        var pending = stream.ReadAsync(new byte[4], cancellation.Token).AsTask();
        await inner.Blocked.WaitAsync(timeout, ct);
        await cancellation.CancelAsync();
        Func<Task> read = () => pending.WaitAsync(timeout, ct);

        // Assert
        await read.ShouldThrowAssignableTo<OperationCanceledException>();
        inner.BlockedReadCancelled.ShouldBeTrue();
        observations.ShouldBe(0);
    }

    [Fact]
    public static async Task Preserves_the_read_failure_without_observing_an_unsuccessful_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var failure = new IOException("unreadable");
        using var inner = new ThrowingReadStream(failure);
        var observations = 0;
        using var stream = new ReadObservingStream(inner.ReadAsync, (_, _) => observations++);

        // Act
        Func<Task> read = async () => await stream.ReadExactlyAsync(new byte[1], ct);
        var thrown = await read.ShouldThrow<IOException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
        observations.ShouldBe(0);
    }

    [Fact]
    public static async Task Asynchronous_disposal_leaves_the_borrowed_stream_open()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new DisposalTrackingStream(new TrackingReadStream([7]));
        using var stream = new ReadObservingStream(inner.ReadAsync, (_, _) => { });

        // Act
        await stream.DisposeAsync();
        var buffer = new byte[1];
        var innerRead = await inner.ReadAsync(buffer, ct);
        Func<Task> read = async () => await stream.ReadExactlyAsync(buffer, ct);

        // Assert
        await read.ShouldThrow<ObjectDisposedException>();
        stream.CanRead.ShouldBeFalse();
        inner.IsDisposed.ShouldBeFalse();
        innerRead.ShouldBe(1);
        buffer[0].ShouldBe((byte)7);
    }

    [Fact]
    public static void Synchronous_disposal_leaves_the_borrowed_stream_open()
    {
        // Arrange
        using var inner = new DisposalTrackingStream(new TrackingReadStream([7]));
        using var stream = new ReadObservingStream(inner.ReadAsync, (_, _) => { });

        // Act
        stream.Dispose();
        var value = inner.ReadByte();

        // Assert
        stream.CanRead.ShouldBeFalse();
        inner.IsDisposed.ShouldBeFalse();
        value.ShouldBe(7);
    }

    [Fact]
    public static void Exposes_only_asynchronous_read_operations()
    {
        // Arrange
        using var inner = new MemoryStream(new byte[1], writable: true);
        using var stream = new ReadObservingStream(inner.ReadAsync, (_, _) => { });

        // Act
        Action syncRead = () => stream.ReadByte();
        Action spanRead = () => stream.ReadExactly(new byte[1].AsSpan());
        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        Action write = () => stream.WriteByte(1);
        Action length = () => _ = stream.Length;
        Action setLength = () => stream.SetLength(1);
        Action getPosition = () => _ = stream.Position;
        Action setPosition = () => stream.Position = 0;
        Action flush = stream.Flush;

        // Assert
        stream.CanRead.ShouldBeTrue();
        stream.CanSeek.ShouldBeFalse();
        stream.CanWrite.ShouldBeFalse();
        syncRead.ShouldThrow<NotSupportedException>();
        spanRead.ShouldThrow<NotSupportedException>();
        seek.ShouldThrow<NotSupportedException>();
        write.ShouldThrow<NotSupportedException>();
        length.ShouldThrow<NotSupportedException>();
        setLength.ShouldThrow<NotSupportedException>();
        getPosition.ShouldThrow<NotSupportedException>();
        setPosition.ShouldThrow<NotSupportedException>();
        flush.ShouldThrow<NotSupportedException>();
    }
}
