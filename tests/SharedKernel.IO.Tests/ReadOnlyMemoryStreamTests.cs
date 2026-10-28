namespace PandaBear.IO.Tests;

/// <summary>
/// Tests ReadOnlyMemoryStream, a copy-free read-only seekable stream over any ReadOnlyMemory of bytes.
/// </summary>
public sealed class ReadOnlyMemoryStreamTests
{
    [Fact]
    public void Reads_all_bytes_with_exact_content()
    {
        // Arrange
        byte[] source = [1, 2, 3, 4, 5];
        using var stream = new ReadOnlyMemoryStream(source);
        var buffer = new byte[8];

        // Act
        var read = stream.Read(buffer, 0, buffer.Length);
        var next = stream.Read(buffer, 0, buffer.Length);

        // Assert
        read.ShouldBe(5);
        next.ShouldBe(0);
        buffer.AsSpan(0, 5).SequenceEqual(source).ShouldBeTrue();
        stream.Position.ShouldBe(5);
        stream.Length.ShouldBe(5);
    }

    [Fact]
    public void Reads_through_span_overload_and_single_bytes()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 9, 8, 7 });
        Span<byte> buffer = stackalloc byte[2];

        // Act
        var read = stream.Read(buffer);
        var single = stream.ReadByte();
        var end = stream.ReadByte();

        // Assert
        read.ShouldBe(2);
        buffer[0].ShouldBe((byte)9);
        buffer[1].ShouldBe((byte)8);
        single.ShouldBe(7);
        end.ShouldBe(-1);
    }

    [Fact]
    public void Does_not_copy_the_backing_array()
    {
        // Arrange
        byte[] source = [1, 2, 3];
        using var stream = new ReadOnlyMemoryStream(source);

        // Act
        source[1] = 99;
        var observed = stream.ReadByte() + stream.ReadByte() * 1000;

        // Assert
        observed.ShouldBe(1 + 99 * 1000);
    }

    [Fact]
    public async Task Reads_memory_that_is_not_array_backed()
    {
        // Arrange
        byte[] source = [4, 5, 6, 7];
        using var owner = new ManagerBackedMemory(source);
        using var stream = new ReadOnlyMemoryStream(owner.Memory);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Assert
        destination.ToArray().SequenceEqual(source).ShouldBeTrue();
    }

    [Fact]
    public void Seek_and_position_follow_stream_rules()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 0, 1, 2, 3, 4, 5 });

        // Act
        var fromBegin = stream.Seek(2, SeekOrigin.Begin);
        var fromCurrent = stream.Seek(1, SeekOrigin.Current);
        var fromEnd = stream.Seek(-1, SeekOrigin.End);
        var valueAtEnd = stream.ReadByte();

        // Assert
        fromBegin.ShouldBe(2);
        fromCurrent.ShouldBe(3);
        fromEnd.ShouldBe(5);
        valueAtEnd.ShouldBe(5);
        stream.CanRead.ShouldBeTrue();
        stream.CanSeek.ShouldBeTrue();
        stream.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Seeking_beyond_the_end_is_allowed_and_reads_nothing()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        var buffer = new byte[4];

        // Act
        stream.Position = 10;
        var read = stream.Read(buffer, 0, buffer.Length);
        var single = stream.ReadByte();

        // Assert
        read.ShouldBe(0);
        single.ShouldBe(-1);
        stream.Position.ShouldBe(10);
    }

    [Fact]
    public void Negative_positions_throw()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        Action setPosition = () => stream.Position = -1;
        Action seek = () => stream.Seek(-4, SeekOrigin.End);
        Action invalidOrigin = () => stream.Seek(0, (SeekOrigin)42);

        // Act
        var positionFailure = setPosition.ShouldThrow<ArgumentOutOfRangeException>();
        var seekFailure = seek.ShouldThrow<IOException>();
        var originFailure = invalidOrigin.ShouldThrow<ArgumentException>();

        // Assert
        positionFailure.ShouldNotBeNull();
        seekFailure.ShouldNotBeNull();
        originFailure.ShouldNotBeNull();
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void Empty_memory_reads_nothing()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(ReadOnlyMemory<byte>.Empty);
        var buffer = new byte[2];

        // Act
        var read = stream.Read(buffer, 0, buffer.Length);

        // Assert
        read.ShouldBe(0);
        stream.Length.ShouldBe(0);
        stream.ReadByte().ShouldBe(-1);
    }

    [Fact]
    public void Zero_count_read_returns_zero_without_moving()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        var buffer = new byte[2];

        // Act
        var read = stream.Read(buffer, 0, 0);

        // Assert
        read.ShouldBe(0);
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task CopyToAsync_into_a_memory_stream_equals_the_source()
    {
        // Arrange
        var source = new byte[200_000];
        Random.Shared.NextBytes(source);
        using var stream = new ReadOnlyMemoryStream(source);
        using var destination = new MemoryStream();

        // Act
        await stream.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Assert
        destination.ToArray().SequenceEqual(source).ShouldBeTrue();
        stream.Position.ShouldBe(source.Length);
    }

    [Fact]
    public void CopyTo_writes_the_remaining_memory_in_one_call()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3, 4 });
        using var destination = new RecordingWriteStream();
        stream.Position = 1;

        // Act
        stream.CopyTo(destination);

        // Assert
        destination.WriteSizes.ShouldBe([3]);
        stream.Position.ShouldBe(4);
    }

    [Fact]
    public async Task ReadAsync_completes_synchronously_with_the_data()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        var buffer = new byte[3];

        // Act
        var pending = stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
        var completedSynchronously = pending.IsCompletedSuccessfully;
        var read = await pending;

        // Assert
        completedSynchronously.ShouldBeTrue();
        read.ShouldBe(3);
        buffer.SequenceEqual(new byte[] { 1, 2, 3 }).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_honors_a_canceled_token()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> action = async () => _ = await stream.ReadAsync(new byte[3].AsMemory(), cancellation.Token);

        // Act
        await action.ShouldThrow<TaskCanceledException>();

        // Assert
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void Write_and_set_length_are_not_supported()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        Action write = () => stream.Write([1], 0, 1);
        Action writeSpan = () => stream.Write(new byte[] { 1 }.AsSpan());
        Action writeByte = () => stream.WriteByte(1);
        Action setLength = () => stream.SetLength(1);

        // Act
        var writeFailure = write.ShouldThrow<NotSupportedException>();
        var spanFailure = writeSpan.ShouldThrow<NotSupportedException>();
        var byteFailure = writeByte.ShouldThrow<NotSupportedException>();
        var lengthFailure = setLength.ShouldThrow<NotSupportedException>();

        // Assert
        writeFailure.ShouldNotBeNull();
        spanFailure.ShouldNotBeNull();
        byteFailure.ShouldNotBeNull();
        lengthFailure.ShouldNotBeNull();
        stream.Length.ShouldBe(3);
    }

    [Fact]
    public void Flush_is_a_no_op()
    {
        // Arrange
        using var stream = new ReadOnlyMemoryStream(new byte[] { 1 });

        // Act
        stream.Flush();

        // Assert
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void Use_after_dispose_throws_and_dispose_is_idempotent()
    {
        // Arrange
        var stream = new ReadOnlyMemoryStream(new byte[] { 1, 2, 3 });
        stream.Dispose();
        Action read = () => _ = stream.Read(new byte[1], 0, 1);
        Action readByte = () => stream.ReadByte();
        Action seek = () => stream.Seek(0, SeekOrigin.Begin);
        Action position = () => _ = stream.Position;
        Action length = () => _ = stream.Length;

        // Act
        stream.Dispose();
        var readFailure = read.ShouldThrow<ObjectDisposedException>();
        var byteFailure = readByte.ShouldThrow<ObjectDisposedException>();
        var seekFailure = seek.ShouldThrow<ObjectDisposedException>();
        var positionFailure = position.ShouldThrow<ObjectDisposedException>();
        var lengthFailure = length.ShouldThrow<ObjectDisposedException>();

        // Assert
        readFailure.ShouldNotBeNull();
        byteFailure.ShouldNotBeNull();
        seekFailure.ShouldNotBeNull();
        positionFailure.ShouldNotBeNull();
        lengthFailure.ShouldNotBeNull();
        stream.CanRead.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_be_rewound_and_read_again()
    {
        // Arrange
        byte[] source = [1, 2, 3, 4, 5, 6];
        using var stream = new ReadOnlyMemoryStream(source);
        using var first = new MemoryStream();
        using var second = new MemoryStream();

        // Act
        await stream.CopyToAsync(first, TestContext.Current.CancellationToken);
        stream.Position = 0;
        await stream.CopyToAsync(second, TestContext.Current.CancellationToken);

        // Assert
        first.ToArray().SequenceEqual(source).ShouldBeTrue();
        second.ToArray().SequenceEqual(source).ShouldBeTrue();
    }
}
