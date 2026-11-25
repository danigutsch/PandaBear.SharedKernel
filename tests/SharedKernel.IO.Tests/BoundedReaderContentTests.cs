namespace SharedKernel.IO.Tests;

/// <summary>Tests whole HTTP content reads that refuse content over a limit.</summary>
public sealed class BoundedReaderContentTests
{
    [Fact]
    public async Task Content_within_the_limit_is_returned()
    {
        // Arrange
        byte[] bytes = [1, 2, 3];
        using var content = new ByteArrayContent(bytes);

        // Act
        var read = await BoundedReader.ReadContent(content, 3, TestContext.Current.CancellationToken);

        // Assert
        read.ShouldBe(bytes);
    }

    [Fact]
    public async Task A_declared_length_over_the_limit_is_refused_without_reading()
    {
        // Arrange
        using var source = new NonSeekableStream(new byte[10]);
        using var content = new StreamContent(source);
        content.Headers.ContentLength = 10;

        // Act
        var read = await BoundedReader.ReadContent(content, 9, TestContext.Current.CancellationToken);

        // Assert
        read.ShouldBeNull();
        source.ReadByte().ShouldBe(0);
    }

    [Fact]
    public async Task Undeclared_content_over_the_limit_is_refused_while_it_is_read()
    {
        // Arrange
        using var content = new StreamContent(new NonSeekableStream(new byte[200_000]));

        // Act
        var read = await BoundedReader.ReadContent(content, 199_999, TestContext.Current.CancellationToken);

        // Assert
        content.Headers.ContentLength.ShouldBeNull();
        read.ShouldBeNull();
    }

    [Fact]
    public async Task Missing_content_and_a_negative_limit_are_refused()
    {
        // Arrange
        using var content = new ByteArrayContent([]);
        var token = TestContext.Current.CancellationToken;

        // Act
        var missing = await Record.ExceptionAsync(() => BoundedReader.ReadContent(null!, 1, token));
        var negative = await Record.ExceptionAsync(() => BoundedReader.ReadContent(content, -1, token));

        // Assert
        missing.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("content");
        negative.ShouldBeOfType<ArgumentOutOfRangeException>().ParamName.ShouldBe("maxBytes");
    }
}
