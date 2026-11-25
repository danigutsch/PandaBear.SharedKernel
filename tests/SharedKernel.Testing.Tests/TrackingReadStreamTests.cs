namespace SharedKernel.Testing.Tests;

/// <summary>Tests the read-size limit of the byte-array stream that records reads and disposal.</summary>
public sealed class TrackingReadStreamTests
{
    [Fact]
    public static void Tracking_read_stream_limits_synchronous_reads_to_the_maximum_bytes_per_read()
    {
        // Arrange
        using var stream = new TrackingReadStream([1, 2, 3, 4, 5]) { MaxBytesPerRead = 2 };
        var buffer = new byte[8];

        // Act
        var first = stream.Read(buffer, 0, buffer.Length);
        var second = stream.Read(buffer.AsSpan(first));

        // Assert
        first.ShouldBe(2);
        second.ShouldBe(2);
        buffer[..4].ShouldBe(new byte[] { 1, 2, 3, 4 });
    }

    [Fact]
    public static async Task Tracking_read_stream_limits_asynchronous_reads_to_the_maximum_bytes_per_read()
    {
        // Arrange
        using var stream = new TrackingReadStream([1, 2, 3, 4, 5]) { MaxBytesPerRead = 2 };
        using var destination = new MemoryStream();

        // Act
        var first = await stream.ReadAsync(new byte[8], TestContext.Current.CancellationToken);
        await stream.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Assert
        first.ShouldBe(2);
        destination.ToArray().ShouldBe(new byte[] { 3, 4, 5 });
        stream.BytesRead.ShouldBe(5);
        stream.ReadCount.ShouldBe(4);
    }

    [Fact]
    public static void Tracking_read_stream_rejects_a_maximum_below_one()
    {
        // Arrange
        Action create = () => _ = new TrackingReadStream([1]) { MaxBytesPerRead = 0 };

        // Act, Assert
        create.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
