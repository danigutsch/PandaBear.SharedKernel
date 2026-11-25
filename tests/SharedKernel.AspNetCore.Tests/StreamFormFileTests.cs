using SharedKernel.Testing;

namespace SharedKernel.AspNetCore.Tests;

/// <summary>Tests the stream-backed form-file fake used for non-seekable uploads.</summary>
public sealed class StreamFormFileTests
{
    [Fact]
    public static void Stream_form_file_hands_out_the_supplied_stream_untouched()
    {
        // Arrange
        using var source = new TrackingReadStream([1, 2, 3]);
        var file = new StreamFormFile(source, 3, "upload.csv", "text/csv", "document");

        // Act
        var opened = file.OpenReadStream();

        // Assert
        opened.ShouldBeSameAs(source);
        opened.CanSeek.ShouldBeFalse();
        source.ReadCount.ShouldBe(0);
    }

    [Fact]
    public static void Stream_form_file_reports_the_declared_metadata_rather_than_the_stream_length()
    {
        // Arrange
        using var source = new TrackingReadStream([1, 2, 3]);

        // Act
        var file = new StreamFormFile(source, 42, "upload.csv", "text/csv", "document");

        // Assert
        file.Length.ShouldBe(42);
        file.FileName.ShouldBe("upload.csv");
        file.Name.ShouldBe("document");
        file.ContentType.ShouldBe("text/csv");
        file.ContentDisposition.ShouldBe("form-data; name=\"document\"; filename=\"upload.csv\"");
        file.Headers["Content-Type"].ToString().ShouldBe("text/csv");
    }

    [Fact]
    public static async Task Stream_form_file_copies_the_remaining_stream_content()
    {
        // Arrange
        using var source = new TrackingReadStream([1, 2, 3]);
        var file = new StreamFormFile(source, 3, "a.bin");
        using var destination = new MemoryStream();

        // Act
        await file.CopyToAsync(destination, TestContext.Current.CancellationToken);

        // Assert
        destination.ToArray().ShouldBe([1, 2, 3]);
        file.ContentType.ShouldBe("application/octet-stream");
        file.Name.ShouldBe("file");
    }

    [Fact]
    public static void Stream_form_file_copies_synchronously()
    {
        // Arrange
        using var source = new TrackingReadStream([4, 5]);
        var file = new StreamFormFile(source, 2, "a.bin");
        using var destination = new MemoryStream();

        // Act
        file.CopyTo(destination);

        // Assert
        destination.ToArray().ShouldBe([4, 5]);
    }

    [Fact]
    public static void Stream_form_file_rejects_a_negative_length()
    {
        // Arrange
        using var source = new TrackingReadStream([]);
        Action act = () => _ = new StreamFormFile(source, -1, "a.bin");

        // Act, Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
