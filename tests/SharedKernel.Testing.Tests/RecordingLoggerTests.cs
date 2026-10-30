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

    [Fact]
    public static void Recording_logger_provider_records_categories_and_keeps_one_ordered_log()
    {
        // Arrange
        using var provider = new RecordingLoggerProvider();

        // Act
        ((RecordingLogger)provider.CreateLogger("First")).Log(LogLevel.Information, default, "one", null, static (state, _) => state);
        ((RecordingLogger)provider.CreateLogger("Second")).Log(LogLevel.Warning, default, "two", null, static (state, _) => state);

        // Assert
        provider.CategoryNames.ShouldBe(["First", "Second"]);
        provider.Records.Select(static record => record.Message).ShouldBe(["one", "two"]);
    }
}
