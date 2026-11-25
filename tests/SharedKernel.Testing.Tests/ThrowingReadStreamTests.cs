namespace SharedKernel.Testing.Tests;

/// <summary>Tests the stream that serves a prefix and then throws a chosen exception.</summary>
public sealed class ThrowingReadStreamTests
{
    [Fact]
    public static async Task Throwing_read_stream_throws_the_exception_on_the_first_read_without_a_prefix()
    {
        // Arrange
        var failure = new IOException("unreadable");
        using var stream = new ThrowingReadStream(failure);
        Func<Task> read = async () => await stream.ReadExactlyAsync(new byte[4], TestContext.Current.CancellationToken);

        // Act
        var thrown = await read.ShouldThrow<IOException>();

        // Assert
        thrown.ShouldBeSameAs(failure);
        stream.ReadCount.ShouldBe(1);
        stream.BytesRead.ShouldBe(0);
    }

    [Fact]
    public static async Task Throwing_read_stream_copies_the_prefix_before_it_throws()
    {
        // Arrange
        using var stream = new ThrowingReadStream([1, 2], new IOException("copy failure"));
        using var destination = new MemoryStream();
        Func<Task> copy = async () => await stream.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Act
        await copy.ShouldThrow<IOException>();

        // Assert
        destination.ToArray().ShouldBe(new byte[] { 1, 2 });
        stream.BytesRead.ShouldBe(2);
    }

    [Fact]
    public static void Throwing_read_stream_throws_from_synchronous_reads_after_the_prefix()
    {
        // Arrange
        using var stream = new ThrowingReadStream([7], new InvalidDataException("bad"));
        var buffer = new byte[4];

        // Act
        var first = stream.Read(buffer, 0, buffer.Length);
        Action read = () => stream.ReadExactly(buffer);

        // Assert
        first.ShouldBe(1);
        read.ShouldThrow<InvalidDataException>();
        stream.ReadCount.ShouldBe(2);
    }

    [Fact]
    public static async Task Throwing_read_stream_honours_a_cancelled_token_before_throwing()
    {
        // Arrange
        using var stream = new ThrowingReadStream(new IOException("unreadable"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> read = async () => await stream.ReadExactlyAsync(new byte[1], cancellation.Token);

        // Act, Assert
        await read.ShouldThrowAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public static async Task Throwing_read_stream_reports_disposal_and_rejects_reads_afterwards()
    {
        // Arrange
        var stream = new ThrowingReadStream(new IOException("unreadable"));

        // Act
        await stream.DisposeAsync();
        Action read = () => stream.ReadByte();

        // Assert
        stream.IsDisposed.ShouldBeTrue();
        stream.CanRead.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
        read.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public static void Throwing_read_stream_rejects_null_arguments()
    {
        // Arrange
        Action nullException = () => _ = new ThrowingReadStream(null!);
        Action nullPrefix = () => _ = new ThrowingReadStream(null!, new IOException());

        // Act, Assert
        nullException.ShouldThrow<ArgumentNullException>();
        nullPrefix.ShouldThrow<ArgumentNullException>();
    }
}
