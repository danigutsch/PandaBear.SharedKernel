using PandaBear.Testing;

namespace PandaBear.ImageSanitizing.Tests;

/// <summary>Tests sanitizing an upload read from a stream: bounded reads, no trust in a declared length, and the stream left open.</summary>
[TestScope(TestScope.Unit)]
public sealed class ImageSanitizerStreamTests
{
    [Fact]
    public async Task A_non_seekable_stream_gives_the_same_image_as_its_bytes()
    {
        // Arrange
        var file = ImageSamples.JpegWithMetadata();
        await using var stream = new TestStream(file, seekable: false);

        // Act
        var result = await ImageSanitizer.Sanitize(stream, ImageSanitizerOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        var image = result.ShouldBeSuccess();
        image.Bytes.ToArray().ShouldBe(ImageSamples.Jpeg());
        stream.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task A_stream_longer_than_the_limit_is_rejected_after_reading_one_byte_past_it()
    {
        // Arrange
        var file = ImageSamples.Concat(ImageSamples.Jpeg(), new byte[100_000]);
        var options = ImageSanitizerOptions.Default with
        {
            MaxBytes = 1000
        };
        await using var stream = new TestStream(file, seekable: false);

        // Act
        var result = await ImageSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeRejected(ImageSanitizer.TooLargeMessage);
        stream.BytesRead.ShouldBe(1001);
    }

    [Fact]
    public async Task A_stream_of_exactly_the_limit_is_accepted()
    {
        // Arrange
        var file = ImageSamples.Png();
        var options = ImageSanitizerOptions.Default with
        {
            MaxBytes = file.Length
        };
        await using var stream = new TestStream(file, seekable: false);

        // Act
        var result = await ImageSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().Bytes.ToArray().ShouldBe(file);
    }

    [Fact]
    public async Task A_seekable_stream_is_read_from_its_current_position()
    {
        // Arrange
        var file = ImageSamples.Concat(ImageSamples.Ascii("junk"), ImageSamples.Webp());
        await using var stream = new TestStream(file, seekable: true) { Position = 4 };

        // Act
        var result = await ImageSanitizer.Sanitize(stream, ImageSanitizerOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().Bytes.ToArray().ShouldBe(ImageSamples.Webp());
    }

    [Fact]
    public async Task A_declared_stream_length_far_beyond_the_limit_does_not_size_the_buffer()
    {
        // Arrange
        var options = ImageSanitizerOptions.Default;
        await using var warmUp = new TestStream(ImageSamples.Png(), seekable: true, declaredLength: long.MaxValue);
        await ImageSanitizer.Sanitize(warmUp, options, TestContext.Current.CancellationToken);
        await using var stream = new TestStream(ImageSamples.Png(), seekable: true, declaredLength: long.MaxValue);
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var result = await ImageSanitizer.Sanitize(stream, options, TestContext.Current.CancellationToken);

        // Assert
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        result.ShouldBeSuccess();
        allocated.ShouldBeLessThan(options.MaxBytes / 4);
    }

    [Fact]
    public async Task A_cancelled_read_throws_operation_cancelled()
    {
        // Arrange
        await using var stream = new TestStream(ImageSamples.Png(), seekable: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(async () => await ImageSanitizer.Sanitize(stream, ImageSanitizerOptions.Default, cancellation.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        stream.BytesRead.ShouldBe(0);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        // Act
        Func<Task> noStream = () => ImageSanitizer.Sanitize(null!, ImageSanitizerOptions.Default, TestContext.Current.CancellationToken);
        Func<Task> noOptions = () => ImageSanitizer.Sanitize(Stream.Null, null!, TestContext.Current.CancellationToken);

        // Assert
        await noStream.ShouldThrow<ArgumentNullException>();
        await noOptions.ShouldThrow<ArgumentNullException>();
    }
}
