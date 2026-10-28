using Microsoft.Extensions.Logging;
using PandaBear.Testing.Logging;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the shared recording and throwing loggers.</summary>
public sealed class RecordingLoggerTests
{
    [Fact]
    public static void Recording_logger_keeps_level_message_state_and_exception()
    {
        // Arrange
        var logger = new RecordingLogger<RecordingLoggerTests>();
        var failure = new InvalidOperationException("boom");

        // Act
        List<KeyValuePair<string, object?>> state = [new("Id", 7)];
        logger.Log(LogLevel.Warning, new EventId(1), state, failure, static (_, _) => "Failed 7");

        // Assert
        var record = logger.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("Failed 7");
        record.Exception.ShouldBeSameAs(failure);
        record.State.ShouldContain(pair => pair.Key == "Id" && Equals(pair.Value, 7));
    }

    [Fact]
    public static void Throwing_logger_throws_the_secret_from_every_call()
    {
        // Arrange
        var logger = new ThrowingLogger<RecordingLoggerTests>();
        Action action = () => logger.Log(LogLevel.Information, new EventId(1), "state", null, static (value, _) => value);

        // Act, Assert
        action.ShouldThrow<InvalidOperationException>().Message.ShouldBe(ThrowingLogger<RecordingLoggerTests>.Secret);
    }
}
