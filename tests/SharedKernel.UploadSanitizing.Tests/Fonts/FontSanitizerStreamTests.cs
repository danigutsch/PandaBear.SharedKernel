using SharedKernel.Testing;

namespace SharedKernel.UploadSanitizing.Tests.Fonts;

/// <summary>Tests validating a font read from a stream: bounded reads, no trust in a declared length, and the stream left open.</summary>
[TestScope(TestScope.Unit)]
public sealed class FontSanitizerStreamTests
{
    [Fact]
    public async Task A_non_seekable_stream_gives_the_same_font_as_its_bytes()
    {
        // Arrange
        var file = Woff2Samples.FontWithMetadataAndPrivateData();
        await using var stream = new TestStream(file, seekable: false);

        // Act
        var result = await FontSanitizer.Sanitize(stream, FontSanitizerOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().Bytes.ToArray().ShouldBe(file);
        stream.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_stream_longer_than_the_limit_is_too_large_after_reading_one_byte_past_it()
    {
        // Arrange
        var options = FontSanitizerOptions.Default with
        {
            MaxBytes = 1000
        };
        await using var stream = new TestStream(Woff2Samples.Font(dataLength: 100_000), seekable: false);

        // Act
        var result = await FontSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeRejected(FontSanitizer.TooLargeMessage);
        stream.BytesRead.ShouldBe(1001);
    }

    [Fact]
    public async Task A_stream_of_exactly_the_limit_is_accepted()
    {
        // Arrange
        var file = Woff2Samples.Font();
        var options = FontSanitizerOptions.Default with
        {
            MaxBytes = file.Length
        };
        await using var stream = new TestStream(file, seekable: false);

        // Act
        var result = await FontSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().Bytes.ToArray().ShouldBe(file);
    }

    [Fact]
    public async Task A_seekable_stream_is_read_from_its_current_position()
    {
        // Arrange
        var file = Woff2Samples.Font();
        await using var stream = new TestStream([.. "junk"u8, .. file], seekable: true) { Position = 4 };

        // Act
        var result = await FontSanitizer.Sanitize(stream, FontSanitizerOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().Bytes.ToArray().ShouldBe(file);
    }

    [Fact]
    public async Task A_declared_stream_length_far_beyond_the_limit_does_not_size_the_buffer()
    {
        // Arrange
        var options = FontSanitizerOptions.Default with
        {
            MaxBytes = FontSanitizerOptions.MaxBytesLimit
        };
        await using var warmUp = new TestStream(Woff2Samples.Font(), seekable: true, declaredLength: long.MaxValue);
        await FontSanitizer.Sanitize(warmUp, options, TestContext.Current.CancellationToken);
        await using var stream = new TestStream(Woff2Samples.Font(), seekable: true, declaredLength: long.MaxValue);
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var result = await FontSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        result.ShouldBeSuccess();
        allocated.ShouldBeLessThan(2 * 1024 * 1024);
    }

    [Fact]
    public async Task A_cancelled_read_throws_operation_cancelled()
    {
        // Arrange
        await using var stream = new TestStream(Woff2Samples.Font(), seekable: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(async () => await FontSanitizer.Sanitize(stream, FontSanitizerOptions.Default, cancellation.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        stream.BytesRead.ShouldBe(0);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Act
        Func<Task> noStream = () => FontSanitizer.Sanitize(null!, FontSanitizerOptions.Default, TestContext.Current.CancellationToken);
        Func<Task> noOptions = () => FontSanitizer.Sanitize(Stream.Null, null!, TestContext.Current.CancellationToken);

        // Assert
        await noStream.ShouldThrow<ArgumentNullException>();
        await noOptions.ShouldThrow<ArgumentNullException>();
    }
}
