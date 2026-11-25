namespace SharedKernel.IO.Tests;

/// <summary>Tests streaming file publication, contention and rollback.</summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public async Task Concurrent_creates_publish_one_complete_file()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "image.webp");
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        var waiting = 0;

        // Act
        byte[] payloads = [1, 2];
        var writes = payloads.Select(value => AtomicFile.CreateIfAbsent(path, async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { value }, token);
            if (Interlocked.Increment(ref waiting) == payloads.Length)
            {
                ready.SetResult();
            }

            await release.Task.WaitAsync(token);
            await stream.WriteAsync(new byte[] { value }, token);
        }, ct).AsTask()).ToArray();
        var completed = await Task.WhenAny([ready.Task, .. writes]).WaitAsync(ct);
        var visibleBeforeCompletion = File.Exists(path);
        release.SetResult();
        var published = await Task.WhenAll(writes);

        // Assert
        completed.ShouldBeSameAs(ready.Task);
        visibleBeforeCompletion.ShouldBeFalse();
        published[0].ShouldNotBe(published[1]);
        var winner = published[0] ? payloads[0] : payloads[1];
        var finalContent = await File.ReadAllBytesAsync(path, ct);
        finalContent.ShouldBe([winner, winner]);
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task Replace_keeps_the_earlier_file_readable_through_an_existing_handle()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "image.webp");
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(path, [1], ct);
        await using var earlierRead = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 4096, FileOptions.Asynchronous);
        var earlierContent = new byte[1];
        byte[]? duringWrite = null;
        string[] temporaryFiles = [];

        // Act
        await AtomicFile.Replace(path, ".media-", async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { 2 }, token);
            duringWrite = await File.ReadAllBytesAsync(path, token);
            temporaryFiles = Directory.GetFiles(temporaryDirectory.Path, ".media-*.tmp");
        }, ct);
        await earlierRead.ReadExactlyAsync(earlierContent, ct);
        var finalContent = await File.ReadAllBytesAsync(path, ct);

        // Assert
        duringWrite.ShouldNotBeNull().ShouldBe([(byte)1]);
        temporaryFiles.ShouldHaveSingleItem();
        earlierContent.ShouldBe([(byte)1]);
        finalContent.ShouldBe([(byte)2]);
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task A_writer_that_rejects_its_content_leaves_the_previous_file_and_removes_partial_bytes()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "image.webp");
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(path, [1], ct);

        // Act
        Func<Task> replace = () => AtomicFile.Replace(path, async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { 2 }, token);
            throw new InvalidDataException("Checksum did not match.");
        }, ct).AsTask();

        // Assert
        await replace.ShouldThrow<InvalidDataException>();
        var finalContent = await File.ReadAllBytesAsync(path, ct);
        finalContent.ShouldBe([(byte)1]);
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task Cancellation_during_the_write_removes_the_temporary_file_without_publishing()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "image.webp");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        Func<Task> create = () => AtomicFile.CreateIfAbsent(path, async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { 1 }, token);
            await cancellation.CancelAsync();
        }, cancellation.Token).AsTask();

        // Assert
        await create.ShouldThrowAssignableTo<OperationCanceledException>();
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBeEmpty();
    }

    [Fact]
    public void Synchronous_replace_publishes_the_bytes_and_leaves_no_temporary_file()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "candidate.json");
        File.WriteAllText(path, "old");

        // Act
        AtomicFile.Replace(path, "new"u8);
        AtomicFile.Replace(Path.Combine(temporaryDirectory.Path, "fresh.json"), "fresh"u8);

        // Assert
        File.ReadAllText(path).ShouldBe("new");
        File.ReadAllText(Path.Combine(temporaryDirectory.Path, "fresh.json")).ShouldBe("fresh");
        Directory.GetFiles(temporaryDirectory.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray().ShouldBe(["candidate.json", "fresh.json"]);
    }

    [Fact]
    public void Synchronous_replace_over_a_directory_fails_and_removes_the_temporary_file()
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "taken");
        Directory.CreateDirectory(path);

        // Act
        var failure = Record.Exception(() => AtomicFile.Replace(path, "x"u8));

        // Assert
        failure.ShouldNotBeNull();
        Directory.GetFiles(temporaryDirectory.Path).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../other/file")]
    [InlineData("nested\\file")]
    public async Task A_prefix_must_be_a_nonempty_plain_filename_fragment(string prefix)
    {
        // Arrange
        var path = Path.Combine(temporaryDirectory.Path, "image.webp");

        // Act
        Func<Task> replace = () => AtomicFile.Replace(path, prefix, (stream, token) =>
            stream.WriteAsync(new byte[] { 1 }, token), TestContext.Current.CancellationToken).AsTask();

        // Assert
        await replace.ShouldThrow<ArgumentException>();
        Directory.GetFileSystemEntries(temporaryDirectory.Path).ShouldBeEmpty();
    }
}
