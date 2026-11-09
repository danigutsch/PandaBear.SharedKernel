namespace PandaBear.Packages.Tests;

/// <summary>Tests the expiring in-memory cache.</summary>
[Trait("Capability", "Packages")]
public sealed class ExpiringCacheTests
{
    [Fact]
    public void An_entry_is_returned_until_its_lifetime_has_passed()
    {
        // Arrange
        var time = new TestTimeProvider(OsvFake.Start);
        var cache = new ExpiringCache<string>(time, TimeSpan.FromMinutes(5));
        cache.Set("k", "v");

        // Act
        time.Advance(TimeSpan.FromMinutes(4));
        var before = cache.TryGet("k", out var value);
        time.Advance(TimeSpan.FromMinutes(1));
        var after = cache.TryGet("k", out _);

        // Assert
        before.ShouldBeTrue();
        value.ShouldBe("v");
        after.ShouldBeFalse();
    }

    [Fact]
    public void A_full_cache_drops_expired_entries_and_otherwise_stores_nothing_new()
    {
        // Arrange
        var time = new TestTimeProvider(OsvFake.Start);
        var cache = new ExpiringCache<int>(time, TimeSpan.FromMinutes(5));
        for (var i = 0; i < ExpiringCache<int>.MaxEntries; i++)
        {
            cache.Set($"k{i}", i);
        }

        // Act
        cache.Set("extra", 1);
        var storedWhileFull = cache.TryGet("extra", out _);
        time.Advance(TimeSpan.FromMinutes(6));
        cache.Set("extra", 2);
        var storedAfterExpiry = cache.TryGet("extra", out var value);

        // Assert
        storedWhileFull.ShouldBeFalse();
        storedAfterExpiry.ShouldBeTrue();
        value.ShouldBe(2);
    }

    [Fact]
    public void A_non_positive_lifetime_is_refused()
    {
        // Act
        Func<object?> create = () => new ExpiringCache<int>(TimeProvider.System, TimeSpan.Zero);

        // Assert
        create.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
