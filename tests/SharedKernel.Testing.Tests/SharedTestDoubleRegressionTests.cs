using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using PandaBear.Testing.Http;
using PandaBear.Testing.Logging;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the edge cases of the shared stream, executable, logger and content doubles.</summary>
public sealed class SharedTestDoubleRegressionTests
{
    [Fact]
    public static async Task Blocking_read_stream_rejects_reads_after_disposal()
    {
        // Arrange
        var stream = new BlockingReadStream();
        await stream.DisposeAsync();
        Func<Task> read = async () => await stream.ReadExactlyAsync(new byte[1].AsMemory(), TestContext.Current.CancellationToken);

        // Act, Assert
        await read.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public static async Task Blocking_read_stream_completes_read_started_when_the_callback_throws()
    {
        // Arrange
        using var stream = new BlockingReadStream(static () => throw new InvalidOperationException("callback"));
        Func<Task> read = async () => await stream.ReadExactlyAsync(new byte[1].AsMemory(), TestContext.Current.CancellationToken);

        // Act
        await read.ShouldThrow<InvalidOperationException>();

        // Assert
        stream.ReadStarted.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public static void Tracking_read_stream_reports_disposal_and_supports_seeking_when_requested()
    {
        // Arrange
        var stream = new TrackingReadStream([1, 2, 3], seekable: true) { Position = 2 };

        // Act
        var value = stream.ReadByte();
        stream.Dispose();

        // Assert
        value.ShouldBe(3);
        stream.CanRead.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
        stream.ReadCount.ShouldBe(1);
    }

    [UnsupportedOSPlatform("windows")]
    [Fact(SkipType = typeof(PosixHost), SkipUnless = nameof(PosixHost.IsPosix), Skip = "POSIX shell scripts are not supported on this host.")]
    public static void Fake_executable_writes_a_script_for_a_bare_file_name()
    {
        // Arrange
        var name = "fake-executable-" + Guid.NewGuid().ToString("N") + ".sh";

        // Act
        FakeExecutable.WriteScript(name, "exit 0\n");
        var exists = File.Exists(name);
        File.Delete(name);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public static void Recording_logger_keeps_scopes_enumerable_state_and_honors_the_minimum_level()
    {
        // Arrange
        var logger = new RecordingLogger { MinimumLevel = LogLevel.Warning };
        IEnumerable<KeyValuePair<string, object?>> state = new Dictionary<string, object?> { ["Id"] = 7 };

        // Act
        logger.BeginScope("outer");
        logger.Log(LogLevel.Information, new EventId(1), state, null, static (_, _) => "message");

        // Assert
        logger.IsEnabled(LogLevel.Information).ShouldBeFalse();
        logger.Scopes.ShouldHaveSingleItem().ShouldBe("outer");
        logger.Records.ShouldHaveSingleItem().State.ShouldContain(pair => pair.Key == "Id");
    }

    [Fact]
    public static async Task Tracking_stream_content_counts_concurrent_serializations()
    {
        // Arrange
        using var source = new MemoryStream();
        using var content = new TrackingStreamContent(source);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(
            () => content.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        // Assert
        content.SerializationCount.ShouldBe(50);
    }
}
