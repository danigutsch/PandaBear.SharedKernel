namespace PandaBear.IO.Tests;

/// <summary>
/// Tests SeekableContent.Create, which copies non-seekable or offset input into seekable memory or file-backed content and passes seekable input at zero through.
/// </summary>
public sealed class SeekableContentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Non_seekable_input_supports_multiple_passes(int threshold)
    {
        // Arrange
        using var source = new NonSeekableStream([1, 2, 3, 4, 5, 6, 7, 8]);

        // Act
        await using var lease = await SeekableContent.Create(source, 8, threshold, TestContext.Current.CancellationToken);
        var initialPosition = lease.Content.Position;
        using var first = new MemoryStream();
        await lease.Content.CopyToAsync(first, TestContext.Current.CancellationToken);
        lease.Content.Position = 0;
        using var second = new MemoryStream();
        await lease.Content.CopyToAsync(second, TestContext.Current.CancellationToken);
        var actualValue = lease.Content is FileStream;

        // Assert
        initialPosition.ShouldBe(0);
        first.ToArray().SequenceEqual(second.ToArray()).ShouldBeTrue();
        first.Length.ShouldBe(8);
        first.ToArray().SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }).ShouldBeTrue();
        actualValue.ShouldBe(threshold < 8);
        source.CanRead.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Exceeding_limit_fails_and_preserves_source_ownership(int threshold)
    {
        // Arrange
        using var source = new NonSeekableStream(new byte[9]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 8, threshold, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<InvalidDataException>();

        // Assert
        source.CanRead.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(81920)]
    [InlineData(100000)]
    [InlineData(250000)]
    public async Task Preserves_bytes_across_chunk_and_spill_boundaries(int threshold)
    {
        // Arrange
        var bytes = Enumerable.Range(0, 250000).Select(index => (byte)(index % 251)).ToArray();
        using var source = new NonSeekableStream(bytes);

        // Act
        await using var lease = await SeekableContent.Create(source, bytes.Length, threshold, TestContext.Current.CancellationToken);
        using var copy = new MemoryStream();
        await lease.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        var actualValue = lease.Content is FileStream;

        // Assert
        copy.ToArray().SequenceEqual(bytes).ShouldBeTrue();
        actualValue.ShouldBe(threshold < bytes.Length);
    }

    [Fact]
    public async Task Pre_canceled_token_does_not_consume_input()
    {
        // Arrange
        using var source = new MemoryStream([1, 2]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 2, 1, cancellation.Token);
        };

        // Act
        await action.ShouldThrow<OperationCanceledException>();

        // Assert
        source.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Empty_content_is_valid_even_with_zero_limit(int threshold)
    {
        // Arrange
        using var source = new MemoryStream();

        // Act
        await using var lease = await SeekableContent.Create(source, 0, threshold, TestContext.Current.CancellationToken);

        // Assert
        lease.Content.Length.ShouldBe(0);
        lease.Content.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Copies_from_current_position_and_reads_only_one_byte_beyond_limit()
    {
        // Arrange
        using var source = new MemoryStream(new byte[100]);
        source.Position = 10;
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 5, 0, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<InvalidDataException>();
        var positionAfterFailure = source.Position;
        source.Position = 98;
        await using var remaining = await SeekableContent.Create(source, 2, 2, TestContext.Current.CancellationToken);

        // Assert
        positionAfterFailure.ShouldBe(16);
        remaining.Content.Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task Negative_limits_fail_before_reading(long maximum, int threshold)
    {
        // Arrange
        using var source = new MemoryStream([1]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, maximum, threshold, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        source.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Null_and_unreadable_sources_are_rejected()
    {
        // Arrange
        Func<Task> missing = async () =>
        {
            await using var lease = await SeekableContent.Create(null!, 1, 1, TestContext.Current.CancellationToken);
        };
        using var source = new MemoryStream();
        source.Close();
        Func<Task> unreadable = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 1, 1, TestContext.Current.CancellationToken);
        };

        // Act, Assert
        await missing.ShouldThrow<ArgumentNullException>();
        await unreadable.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_failure_and_cancellation_after_spilling_preserve_input_ownership(bool cancel)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        using var source = new FailingReadStream(cancel ? cancellation : null);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 200000, 0, cancellation.Token);
        };

        // Act
        if (cancel)
        {
            await action.ShouldThrow<OperationCanceledException>();
        }
        else
        {
            await action.ShouldThrow<IOException>();
        }

        // Assert
        source.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task Synchronous_disposal_deletes_file()
    {
        // Arrange
        using var source = new NonSeekableStream([1]);
        var lease = await SeekableContent.Create(source, 1, 0, TestContext.Current.CancellationToken);
        var path = lease.Content.ShouldBeOfType<FileStream>().Name;

        // Act
        ((IDisposable)lease).Dispose();

        // Assert
        File.Exists(path).ShouldBeFalse();
        lease.Content.CanRead.ShouldBeFalse();
    }

    [Fact]
    public async Task File_is_deleted_on_disposal()
    {
        // Arrange
        using var source = new NonSeekableStream([1, 2]);
        var lease = await SeekableContent.Create(source, 2, 0, TestContext.Current.CancellationToken);
        var file = lease.Content.ShouldBeOfType<FileStream>();
        var path = file.Name;

        // Act
        await lease.DisposeAsync();
        var fileExistsAfterDisposal = File.Exists(path);
        await lease.DisposeAsync();

        // Assert
        fileExistsAfterDisposal.ShouldBeFalse();
        source.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task Seekable_source_at_zero_is_passed_through_without_copy()
    {
        // Arrange
        using var source = new MemoryStream([1, 2, 3, 4]);

        // Act
        await using var lease = await SeekableContent.Create(source, 4, 0, TestContext.Current.CancellationToken);

        // Assert
        lease.Content.ShouldBeSameAs(source);
        lease.Content.ShouldNotBeOfType<FileStream>();
        lease.Content.Position.ShouldBe(0);
        lease.Content.Length.ShouldBe(4);
    }

    [Fact]
    public async Task Passed_through_lease_disposal_leaves_source_open_and_usable()
    {
        // Arrange
        using var source = new MemoryStream([1, 2, 3]);
        var lease = await SeekableContent.Create(source, 3, 0, TestContext.Current.CancellationToken);

        // Act
        ((IDisposable)lease).Dispose();
        await lease.DisposeAsync();
        source.Position = 1;

        // Assert
        source.CanRead.ShouldBeTrue();
        source.ReadByte().ShouldBe(2);
    }

    [Fact]
    public async Task Passed_through_source_accepts_exact_limit_and_supports_second_pass()
    {
        // Arrange
        var bytes = new byte[200000];
        using var source = new MemoryStream(bytes);

        // Act
        await using var lease = await SeekableContent.Create(source, bytes.Length, 0, TestContext.Current.CancellationToken);
        lease.Content.Position = 0;
        using var copy = new MemoryStream();
        await lease.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);

        // Assert
        copy.Length.ShouldBe(bytes.Length);
    }

    [Fact]
    public async Task Passed_through_source_over_limit_fails_restores_position_and_stays_open()
    {
        // Arrange
        using var source = new MemoryStream(new byte[9]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 8, 0, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<InvalidDataException>();

        // Assert
        source.CanRead.ShouldBeTrue();
        source.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Passed_through_negative_limit_fails_before_reading()
    {
        // Arrange
        using var source = new MemoryStream([1]);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, -1, 0, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        source.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_during_pass_through_count_restores_position_and_keeps_source_open()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        using var source = new FailingReadStream(cancellation);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 200000, 0, cancellation.Token);
        };

        // Act
        await action.ShouldThrow<OperationCanceledException>();

        // Assert
        source.CanRead.ShouldBeTrue();
        source.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Seekable_source_at_non_zero_position_is_copied_from_that_position()
    {
        // Arrange
        using var source = new MemoryStream([1, 2, 3, 4]);
        source.Position = 2;

        // Act
        await using var lease = await SeekableContent.Create(source, 2, 4, TestContext.Current.CancellationToken);

        // Assert
        lease.Content.ShouldNotBeSameAs(source);
        lease.Content.Position.ShouldBe(0);
        lease.Content.Length.ShouldBe(2);
        lease.Content.ReadByte().ShouldBe(3);
        source.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task Empty_seekable_source_is_passed_through_even_with_zero_limit()
    {
        // Arrange
        using var source = new MemoryStream();

        // Act
        await using var lease = await SeekableContent.Create(source, 0, 0, TestContext.Current.CancellationToken);

        // Assert
        lease.Content.ShouldBeSameAs(source);
        lease.Content.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Seekable_source_whose_length_disagrees_with_bytes_read_is_rejected()
    {
        // Arrange
        using var source = new MisreportedLengthStream([1, 2, 3], 10);
        Func<Task> action = async () =>
        {
            await using var lease = await SeekableContent.Create(source, 100, 0, TestContext.Current.CancellationToken);
        };

        // Act
        await action.ShouldThrow<InvalidDataException>();

        // Assert
        source.CanRead.ShouldBeTrue();
        source.Position.ShouldBe(0);
    }
}
