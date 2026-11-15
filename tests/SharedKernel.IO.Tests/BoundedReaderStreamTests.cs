namespace PandaBear.IO.Tests;

/// <summary>Tests whole-stream reads that refuse content over a limit.</summary>
public sealed class BoundedReaderStreamTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Content_within_the_limit_is_returned(int extra)
    {
        // Arrange
        byte[] bytes = [1, 2, 3, 4, 5];
        using var stream = new NonSeekableStream(bytes);

        // Act
        var content = await BoundedReader.ReadToEnd(stream, bytes.Length + extra, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBe(bytes);
    }

    [Fact]
    public async Task Non_seekable_content_over_the_limit_is_refused()
    {
        // Arrange
        using var stream = new NonSeekableStream(new byte[200_000]);

        // Act
        var content = await BoundedReader.ReadToEnd(stream, 199_999, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBeNull();
    }

    [Fact]
    public async Task Seekable_content_over_the_limit_is_refused_without_reading()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[10]);

        // Act
        var content = await BoundedReader.ReadToEnd(stream, 9, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBeNull();
        stream.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Reading_starts_at_the_current_position()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3, 4]) { Position = 2 };

        // Act
        var content = await BoundedReader.ReadToEnd(stream, 2, TestContext.Current.CancellationToken);

        // Assert
        content.ShouldBe(new byte[] { 3, 4 });
    }

    [Fact]
    public async Task A_missing_stream_is_rejected()
    {
        // Act
        Func<Task> missing = () => BoundedReader.ReadToEnd(null!, 1, TestContext.Current.CancellationToken);

        // Assert
        await missing.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task A_negative_limit_is_rejected()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var exception = await Record.ExceptionAsync(async () => await BoundedReader.ReadToEnd(stream, -1, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<ArgumentOutOfRangeException>();
    }
}
