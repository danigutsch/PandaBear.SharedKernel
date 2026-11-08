using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the bounded reading of a process's two output streams.</summary>
[Trait("Capability", "Packages")]
public sealed class ProcessStreamReadersTests
{
    private static readonly TimeSpan Drain = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Both_streams_are_returned_when_they_end_under_the_limit()
    {
        // Arrange
        using var overflow = new CancellationTokenSource();
        await using var readers = ProcessStreamReaders.Start(Reader("out"), Reader("err"), overflow, TestContext.Current.CancellationToken);

        // Act
        var (output, error) = await readers.Completed(Drain, TestContext.Current.CancellationToken);

        // Assert
        output.ShouldBe("out");
        error.ShouldBe("err");
        overflow.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stream_of_exactly_the_limit_is_returned_whole_and_one_more_character_is_an_error(bool onOutput)
    {
        // Arrange
        const int Limit = 8 * 1024 * 1024;
        using var atLimitSource = new CancellationTokenSource();
        await using var atLimit = onOutput
            ? ProcessStreamReaders.Start(Reader(new string('a', Limit)), Reader(string.Empty), atLimitSource, TestContext.Current.CancellationToken)
            : ProcessStreamReaders.Start(Reader(string.Empty), Reader(new string('a', Limit)), atLimitSource, TestContext.Current.CancellationToken);
        using var overSource = new CancellationTokenSource();
        await using var over = onOutput
            ? ProcessStreamReaders.Start(Reader(new string('a', Limit + 1)), Reader(string.Empty), overSource, TestContext.Current.CancellationToken)
            : ProcessStreamReaders.Start(Reader(string.Empty), Reader(new string('a', Limit + 1)), overSource, TestContext.Current.CancellationToken);

        // Act
        var (output, error) = await atLimit.Completed(Drain, TestContext.Current.CancellationToken);
        Func<Task> overAct = () => over.Completed(Drain, TestContext.Current.CancellationToken);

        // Assert
        var kept = output.Length + error.Length;
        kept.ShouldBe(Limit, "Exactly the limit is kept whole.");
        atLimitSource.IsCancellationRequested.ShouldBeFalse();
        await overAct.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stream_a_little_over_the_limit_is_an_error_even_when_the_reads_finished_before_completed_was_called(bool onOutput)
    {
        // Arrange: the whole stream is read at once, so every read has finished before Completed is called.
        using var overflow = new CancellationTokenSource();
        var big = Reader(new string('a', 8 * 1024 * 1024 + 100));
        await using var readers = onOutput
            ? ProcessStreamReaders.Start(big, Reader(string.Empty), overflow, TestContext.Current.CancellationToken)
            : ProcessStreamReaders.Start(Reader(string.Empty), big, overflow, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        // Act
        Func<Task> act = () => readers.Completed(Drain, TestContext.Current.CancellationToken);

        // Assert
        var exception = await act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("more output than the limit", StringComparison.Ordinal);
        overflow.IsCancellationRequested.ShouldBeTrue("Going over the limit ends the whole run.");
    }
}
