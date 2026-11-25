namespace SharedKernel.Testing.Tests;

/// <summary>Tests the non-seekable stream that serves one chunk per released read.</summary>
public sealed class GatedChunkReadStreamTests
{
    private static readonly TimeSpan FailureGuard = TimeSpan.FromSeconds(10);

    [Fact]
    public static async Task Gated_chunk_read_stream_serves_one_chunk_per_released_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new GatedChunkReadStream([1, 2, 3, 4, 5], chunkSize: 2);
        var buffer = new byte[16];

        // Act
        var firstRead = stream.ReadAsync(buffer, ct).AsTask();
        await stream.WaitForPendingRead(ct).WaitAsync(FailureGuard, ct);
        var completedBeforeRelease = firstRead.IsCompleted;
        stream.ReleaseRead();
        var first = await firstRead.WaitAsync(FailureGuard, ct);
        var secondRead = stream.ReadAsync(buffer.AsMemory(first), ct).AsTask();
        await stream.WaitForPendingRead(ct).WaitAsync(FailureGuard, ct);
        var bytesBeforeSecondRelease = stream.BytesRead;
        stream.ReleaseRead();
        var second = await secondRead.WaitAsync(FailureGuard, ct);

        // Assert
        completedBeforeRelease.ShouldBeFalse();
        first.ShouldBe(2);
        bytesBeforeSecondRelease.ShouldBe(2);
        second.ShouldBe(2);
        buffer[..4].ShouldBe(new byte[] { 1, 2, 3, 4 });
        stream.BytesRead.ShouldBe(4);
        stream.ReadCount.ShouldBe(2);
        stream.LargestRequestedRead.ShouldBe(16);
    }

    [Fact]
    public static async Task Gated_chunk_read_stream_banks_releases_made_before_the_read()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new GatedChunkReadStream([1, 2, 3], chunkSize: 2);
        stream.ReleaseRead();
        stream.ReleaseRead();
        var buffer = new byte[3];

        // Act
        var first = await stream.ReadAsync(buffer, ct);
        var second = await stream.ReadAsync(buffer.AsMemory(first), ct);

        // Assert
        var total = first + second;
        total.ShouldBe(3);
        buffer.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public static async Task Gated_chunk_read_stream_lets_every_read_through_after_release_all_and_reports_the_end()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new GatedChunkReadStream([1, 2, 3, 4, 5], chunkSize: 2);
        stream.ReleaseAll();
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination, ct);

        // Assert
        destination.ToArray().ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
        stream.ReadCount.ShouldBe(4);
        stream.CanSeek.ShouldBeFalse();
    }

    [Fact]
    public static async Task Gated_chunk_read_stream_cancels_a_waiting_read_without_serving_data()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new GatedChunkReadStream([1, 2, 3], chunkSize: 2);
        using var cancellation = new CancellationTokenSource();
        var read = stream.ReadAsync(new byte[3], cancellation.Token).AsTask();
        await stream.WaitForPendingRead(ct).WaitAsync(FailureGuard, ct);

        // Act
        await cancellation.CancelAsync();
        Func<Task> pending = () => read.WaitAsync(FailureGuard, ct);

        // Assert
        await pending.ShouldThrowAssignableTo<OperationCanceledException>();
        stream.BytesRead.ShouldBe(0);
    }

    [Fact]
    public static async Task Gated_chunk_read_stream_fails_a_waiting_read_with_object_disposed_when_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var stream = new GatedChunkReadStream([1, 2, 3], chunkSize: 2);
        var read = stream.ReadAsync(new byte[3], ct).AsTask();
        await stream.WaitForPendingRead(ct).WaitAsync(FailureGuard, ct);

        // Act
        await stream.DisposeAsync();
        stream.Close();
        Func<Task> pending = () => read.WaitAsync(FailureGuard, ct);

        // Assert
        await pending.ShouldThrow<ObjectDisposedException>();
        stream.IsDisposed.ShouldBeTrue();
        stream.CanRead.ShouldBeFalse();
        stream.BytesRead.ShouldBe(0);
    }

    [Fact]
    public static async Task Gated_chunk_read_stream_blocks_a_synchronous_read_until_released()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var stream = new GatedChunkReadStream([1, 2, 3], chunkSize: 2);
        var buffer = new byte[3];

        // Act
        var read = Task.Factory.StartNew(
            () => stream.Read(buffer, 0, buffer.Length),
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await stream.WaitForPendingRead(ct).WaitAsync(FailureGuard, ct);
        var completedBeforeRelease = read.IsCompleted;
        stream.ReleaseRead();
        var count = await read.WaitAsync(FailureGuard, ct);

        // Assert
        completedBeforeRelease.ShouldBeFalse();
        count.ShouldBe(2);
        stream.BytesRead.ShouldBe(2);
    }

    [Fact]
    public static void Gated_chunk_read_stream_rejects_invalid_arguments()
    {
        // Arrange
        Action nullContent = () => _ = new GatedChunkReadStream(null!, 1);
        Action zeroChunk = () => _ = new GatedChunkReadStream([], 0);

        // Act, Assert
        nullContent.ShouldThrow<ArgumentNullException>();
        zeroChunk.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
