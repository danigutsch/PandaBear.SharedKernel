namespace PandaBear.IO.Tests;

public sealed class SeekableContentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Non_seekable_input_supports_multiple_passes(int threshold)
    {
        using var source = new NonSeekableStream([1, 2, 3, 4, 5, 6, 7, 8]);
        await using var lease = await SeekableContent.Create(source, 8, threshold, TestContext.Current.CancellationToken);
        lease.Content.Position.ShouldBe(0);
        using var first = new MemoryStream();
        await lease.Content.CopyToAsync(first, TestContext.Current.CancellationToken);
        lease.Content.Position = 0;
        using var second = new MemoryStream();
        await lease.Content.CopyToAsync(second, TestContext.Current.CancellationToken);
        first.ToArray().SequenceEqual(second.ToArray()).ShouldBeTrue();
        first.Length.ShouldBe(8);
        first.ToArray().SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).ShouldBeTrue();
        var actualValue = lease.Content is FileStream;
        actualValue.ShouldBe(threshold < 8);
        source.CanRead.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Exceeding_limit_fails_and_preserves_source_ownership(int threshold)
    {
        using var source = new NonSeekableStream(new byte[9]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 8, threshold, TestContext.Current.CancellationToken);
        };
        await action.ShouldThrow<InvalidDataException>();
        source.CanRead.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(81920)]
    [InlineData(100000)]
    [InlineData(250000)]
    public async Task Preserves_bytes_across_chunk_and_spill_boundaries(int threshold)
    {
        var bytes = Enumerable.Range(0, 250000).Select(index => (byte)(index % 251)).ToArray();
        using var source = new NonSeekableStream(bytes);
        await using var lease = await SeekableContent.Create(source, bytes.Length, threshold, TestContext.Current.CancellationToken);
        using var copy = new MemoryStream();
        await lease.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        copy.ToArray().SequenceEqual(bytes).ShouldBeTrue();
        var actualValue = lease.Content is FileStream;
        actualValue.ShouldBe(threshold < bytes.Length);
    }

    [Fact]
    public async Task Pre_canceled_token_does_not_consume_input()
    {
        using var source = new MemoryStream([1, 2]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 2, 1, cancellation.Token);
        };
        await action.ShouldThrow<OperationCanceledException>();
        source.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Empty_content_is_valid_even_with_zero_limit(int threshold)
    {
        using var source = new MemoryStream();
        await using var lease = await SeekableContent.Create(source, 0, threshold, TestContext.Current.CancellationToken);
        lease.Content.Length.ShouldBe(0);
        lease.Content.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Copies_from_current_position_and_reads_only_one_byte_beyond_limit()
    {
        using var source = new MemoryStream(new byte[100]);
        source.Position = 10;
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 5, 0, TestContext.Current.CancellationToken);
        };
        await action.ShouldThrow<InvalidDataException>();
        source.Position.ShouldBe(16);
        source.Position = 98;
        await using var remaining = await SeekableContent.Create(source, 2, 2, TestContext.Current.CancellationToken);
        remaining.Content.Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task Negative_limits_fail_before_reading(long maximum, int threshold)
    {
        using var source = new MemoryStream([1]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, maximum, threshold, TestContext.Current.CancellationToken);
        };
        await action.ShouldThrow<ArgumentOutOfRangeException>();
        source.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Null_and_unreadable_sources_are_rejected()
    {
        Func<Task> missing = async () =>
        {
            await using var lease = await SeekableContent.Create(null!, 1, 1, TestContext.Current.CancellationToken);
        };
        await missing.ShouldThrow<ArgumentNullException>();
        using var source = new MemoryStream();
        source.Close();
        Func<Task> unreadable = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 1, 1, TestContext.Current.CancellationToken);
        };
        await unreadable.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_failure_and_cancellation_after_spilling_preserve_input_ownership(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new FailingReadStream(cancel ? cancellation : null);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 200000, 0, cancellation.Token);
        };
        if (cancel)
        {
            await action.ShouldThrow<OperationCanceledException>();
        }
        else
        {
            await action.ShouldThrow<IOException>();
        }

        source.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task Synchronous_disposal_deletes_file()
    {
        using var source = new MemoryStream([1]);
        var lease = await SeekableContent.Create(source, 1, 0, TestContext.Current.CancellationToken);
        var path = lease.Content.ShouldBeOfType<FileStream>().Name;
        ((IDisposable)lease).Dispose();
        File.Exists(path).ShouldBeFalse();
        lease.Content.CanRead.ShouldBeFalse();
    }

    [Fact]
    public async Task File_is_deleted_on_disposal()
    {
        using var source = new MemoryStream([1, 2]);
        var lease = await SeekableContent.Create(source, 2, 0, TestContext.Current.CancellationToken);
        var file = lease.Content.ShouldBeOfType<FileStream>();
        var path = file.Name;
        await lease.DisposeAsync();
        File.Exists(path).ShouldBeFalse();
        await lease.DisposeAsync();
        source.CanRead.ShouldBeTrue();
    }
}
