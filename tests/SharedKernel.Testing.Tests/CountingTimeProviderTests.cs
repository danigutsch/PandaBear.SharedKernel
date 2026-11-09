using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the time provider that counts how often the current time is read.</summary>
public sealed class CountingTimeProviderTests
{
    private static readonly DateTimeOffset Instant = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public static void Counting_time_provider_counts_each_read_and_reports_the_inner_time()
    {
        // Arrange
        var provider = new CountingTimeProvider(new FixedClock(Instant));

        // Act
        var first = provider.GetUtcNow();
        var second = provider.GetUtcNow();

        // Assert
        first.ShouldBe(Instant);
        second.ShouldBe(Instant);
        provider.Reads.ShouldBe(2);
    }

    [Fact]
    public static void Counting_time_provider_counts_a_local_time_read_once_and_uses_the_inner_zone()
    {
        // Arrange
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus-three", TimeSpan.FromHours(3), "plus-three", "plus-three");
        var provider = new CountingTimeProvider(new FixedClock(Instant, zone));

        // Act
        var local = provider.GetLocalNow();

        // Assert
        local.Offset.ShouldBe(TimeSpan.FromHours(3));
        local.UtcDateTime.ShouldBe(Instant.UtcDateTime);
        provider.LocalTimeZone.ShouldBeSameAs(zone);
        provider.Reads.ShouldBe(1);
    }

    [Fact]
    public static void Reset_sets_the_read_count_back_to_zero()
    {
        // Arrange
        var provider = new CountingTimeProvider(new FixedClock(Instant));
        provider.GetUtcNow();

        // Act
        provider.Reset();

        // Assert
        provider.Reads.ShouldBe(0);
    }

    [Fact]
    public static void Counting_time_provider_without_an_inner_provider_reads_the_system_clock()
    {
        // Arrange
        var provider = new CountingTimeProvider();
        var before = TimeProvider.System.GetUtcNow();

        // Act
        var now = provider.GetUtcNow();

        // Assert
        now.ShouldBeGreaterThanOrEqualTo(before);
        provider.Reads.ShouldBe(1);
    }

    [Fact]
    public static async Task Counting_time_provider_counts_concurrent_reads_without_losing_any()
    {
        // Arrange
        var provider = new CountingTimeProvider(new FixedClock(Instant));

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var read = 0; read < 1000; read++)
            {
                provider.GetUtcNow();
            }
        }, TestContext.Current.CancellationToken)));

        // Assert
        provider.Reads.ShouldBe(8000);
    }

    [Fact]
    public static void Counting_time_provider_rejects_a_null_inner_provider()
    {
        // Arrange
        Action construct = () => _ = new CountingTimeProvider(null!);

        // Act
        var exception = construct.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("inner");
    }

    [Fact]
    public static void Counting_time_provider_creates_timers_on_the_inner_provider()
    {
        // Arrange
        var fake = new FakeTimeProvider(Instant);
        var provider = new CountingTimeProvider(fake);
        var fired = 0;
        using var timer = provider.CreateTimer(_ => Interlocked.Increment(ref fired), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);

        // Act
        fake.Advance(TimeSpan.FromSeconds(5));

        // Assert
        fired.ShouldBe(1);
    }
}
