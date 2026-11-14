namespace PandaBear.Testing.Tests;

/// <summary>Tests the stream that serves a prefix, then blocks until it is released, cancelled or disposed.</summary>
public sealed class BlockingReadStreamTests
{
    private static readonly TimeSpan FailureGuard = TimeSpan.FromSeconds(10);

    [Fact]
    public static async Task Blocking_read_stream_serves_the_prefix_then_blocks_until_released_and_serves_the_remainder()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream([1, 2], [3, 4]);
        var buffer = new byte[8];

        // Act
        var first = await stream.ReadAsync(buffer, ct);
        var blockedRead = stream.ReadAsync(buffer.AsMemory(first), ct).AsTask();
        await stream.Blocked.WaitAsync(FailureGuard, ct);
        var completedBeforeRelease = blockedRead.IsCompleted;
        stream.Release();
        var second = await blockedRead.WaitAsync(FailureGuard, ct);
        var end = await stream.ReadAsync(buffer.AsMemory(first + second), ct);

        // Assert
        first.ShouldBe(2);
        completedBeforeRelease.ShouldBeFalse();
        second.ShouldBe(2);
        end.ShouldBe(0);
        buffer[..4].ShouldBe(new byte[] { 1, 2, 3, 4 });
        stream.BytesRead.ShouldBe(4);
    }

    [Fact]
    public static async Task Blocking_read_stream_copies_the_prefix_before_it_blocks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream([1, 2], [3]);
        using var destination = new MemoryStream();

        // Act
        var copy = Task.Run(async () => await stream.CopyToAsync(destination, ct), ct);
        await stream.Blocked.WaitAsync(FailureGuard, ct);
        var copiedWhileBlocked = destination.ToArray();
        stream.Release();
        await copy.WaitAsync(FailureGuard, ct);

        // Assert
        copiedWhileBlocked.ShouldBe(new byte[] { 1, 2 });
        destination.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public static async Task Blocking_read_stream_cancels_the_blocked_read_and_records_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream("i"u8.ToArray(), []);
        using var cancellation = new CancellationTokenSource();
        var buffer = new byte[4];
        _ = await stream.ReadAsync(buffer, ct);

        // Act
        var blockedRead = stream.ReadAsync(buffer, cancellation.Token).AsTask();
        await stream.Blocked.WaitAsync(FailureGuard, ct);
        var cancelledBeforeCancel = stream.BlockedReadCancelled;
        await cancellation.CancelAsync();
        Func<Task> read = () => blockedRead.WaitAsync(FailureGuard, ct);

        // Assert
        await read.ShouldThrowAssignableTo<OperationCanceledException>();
        cancelledBeforeCancel.ShouldBeFalse();
        stream.BlockedReadCancelled.ShouldBeTrue();
        stream.BytesRead.ShouldBe(1);
    }

    [Fact]
    public static async Task Blocking_read_stream_without_content_blocks_its_first_read_until_cancelled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream();
        using var cancellation = new CancellationTokenSource();

        // Act
        var blockedRead = stream.ReadAsync(new byte[1], cancellation.Token).AsTask();
        await stream.ReadStarted.WaitAsync(FailureGuard, ct);
        await stream.Blocked.WaitAsync(FailureGuard, ct);
        await cancellation.CancelAsync();
        Func<Task> read = () => blockedRead.WaitAsync(FailureGuard, ct);

        // Assert
        await read.ShouldThrowAssignableTo<OperationCanceledException>();
        stream.BlockedReadCancelled.ShouldBeTrue();
    }

    [Fact]
    public static async Task Blocking_read_stream_does_not_block_once_released_before_the_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream([1], [2]);
        stream.Release();
        var buffer = new byte[2];

        // Act
        await stream.ReadExactlyAsync(buffer, ct);

        // Assert
        buffer.ShouldBe(new byte[] { 1, 2 });
        stream.Blocked.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public static async Task Blocking_read_stream_fails_a_blocked_read_with_object_disposed_when_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stream = new BlockingReadStream([], [1]);
        var blockedRead = stream.ReadAsync(new byte[1], ct).AsTask();
        await stream.Blocked.WaitAsync(FailureGuard, ct);

        // Act
        await stream.DisposeAsync();
        Func<Task> read = () => blockedRead.WaitAsync(FailureGuard, ct);

        // Assert
        await read.ShouldThrow<ObjectDisposedException>();
        stream.IsDisposed.ShouldBeTrue();
        stream.CanRead.ShouldBeFalse();
        stream.Disposed.IsCompletedSuccessfully.ShouldBeTrue();
        stream.BlockedReadCancelled.ShouldBeFalse();
    }

    [Fact]
    public static async Task Blocking_read_stream_blocks_a_synchronous_read_until_released()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new BlockingReadStream([1], [2]);
        var buffer = new byte[2];
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var blockedRead = Task.Factory.StartNew(
            () =>
            {
                first.SetResult(stream.Read(buffer, 0, 2));
                return stream.Read(buffer, 1, 1);
            },
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await stream.Blocked.WaitAsync(FailureGuard, ct);
        var completedBeforeRelease = blockedRead.IsCompleted;
        stream.Release();
        var second = await blockedRead.WaitAsync(FailureGuard, ct);

        // Assert
        var firstCount = await first.Task;
        firstCount.ShouldBe(1);
        completedBeforeRelease.ShouldBeFalse();
        second.ShouldBe(1);
        buffer.ShouldBe(new byte[] { 1, 2 });
    }

    [Fact]
    public static async Task Blocking_read_stream_unblocks_a_synchronous_read_with_object_disposed_when_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stream = new BlockingReadStream();
        var blockedRead = Task.Factory.StartNew(
            () => stream.ReadByte(),
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await stream.Blocked.WaitAsync(FailureGuard, ct);

        // Act
        stream.Close();
        Func<Task> read = () => blockedRead.WaitAsync(FailureGuard, ct);

        // Assert
        await read.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public static void Blocking_read_stream_rejects_null_content()
    {
        // Arrange
        Action nullPrefix = () => _ = new BlockingReadStream(null!, []);
        Action nullRemainder = () => _ = new BlockingReadStream([], null!);

        // Act, Assert
        nullPrefix.ShouldThrow<ArgumentNullException>();
        nullRemainder.ShouldThrow<ArgumentNullException>();
    }
}
