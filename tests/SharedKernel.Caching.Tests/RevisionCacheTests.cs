using Microsoft.Extensions.Time.Testing;
using PandaBear.Testing;

namespace PandaBear.Caching.Tests;

/// <summary>
/// Tests the revision-keyed read cache: values are reused while the shared revision is unchanged, the revision is read at most once per
/// interval, and a newer revision replaces every cached value.
/// </summary>
[TestScope(TestScope.Unit)]
public sealed class RevisionCacheTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task The_first_read_loads_the_value_at_the_current_revision()
    {
        // Arrange
        var source = new RevisionSource { Revision = 7 };
        var cache = source.Cache(Interval, new FakeTimeProvider());

        // Act
        var value = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("pt@7");
        source.Loads.ShouldBe([("pt", 7L)]);
    }

    [Fact]
    public async Task Within_the_interval_the_cached_value_is_returned_without_reading_the_revision_again()
    {
        // Arrange
        var source = new RevisionSource { Revision = 1 };
        var time = new FakeTimeProvider();
        var cache = source.Cache(Interval, time);
        await cache.Get("pt", TestContext.Current.CancellationToken);
        source.Revision = 2;
        time.Advance(Interval - TimeSpan.FromTicks(1));

        // Act
        var value = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("pt@1");
        source.RevisionReads.ShouldBe(1);
        source.Loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task After_the_interval_an_unchanged_revision_keeps_the_cached_value()
    {
        // Arrange
        var source = new RevisionSource { Revision = 3 };
        var time = new FakeTimeProvider();
        var cache = source.Cache(Interval, time);
        await cache.Get("pt", TestContext.Current.CancellationToken);
        time.Advance(Interval);

        // Act
        var value = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("pt@3");
        source.RevisionReads.ShouldBe(2);
        source.Loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task After_the_interval_a_newer_revision_reloads_every_key()
    {
        // Arrange
        var source = new RevisionSource { Revision = 3 };
        var time = new FakeTimeProvider();
        var cache = source.Cache(Interval, time);
        await cache.Get("pt", TestContext.Current.CancellationToken);
        await cache.Get("en", TestContext.Current.CancellationToken);
        source.Revision = 4;
        time.Advance(Interval);

        // Act
        var portuguese = await cache.Get("pt", TestContext.Current.CancellationToken);
        var english = await cache.Get("en", TestContext.Current.CancellationToken);

        // Assert
        (portuguese, english).ShouldBe(("pt@4", "en@4"));
        source.Loads.ShouldBe([("pt", 3L), ("en", 3L), ("pt", 4L), ("en", 4L)]);
    }

    [Fact]
    public async Task A_revision_that_goes_back_is_ignored()
    {
        // Arrange
        var source = new RevisionSource { Revision = 9 };
        var time = new FakeTimeProvider();
        var cache = source.Cache(Interval, time);
        await cache.Get("pt", TestContext.Current.CancellationToken);
        source.Revision = 8;
        time.Advance(Interval);

        // Act
        var value = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("pt@9");
        source.Loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_load_is_not_cached()
    {
        // Arrange
        var source = new RevisionSource { Revision = 1, LoadFailure = new InvalidOperationException("database down") };
        var cache = source.Cache(Interval, new FakeTimeProvider());
        Func<Task> failing = async () => await cache.Get("pt", TestContext.Current.CancellationToken);
        await failing.ShouldThrow<InvalidOperationException>();

        // Act
        var value = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("pt@1");
        source.Loads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_slow_load_of_an_older_revision_does_not_replace_the_newer_value()
    {
        // Arrange
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new RevisionSource { Revision = 1, PendingLoad = pending };
        var time = new FakeTimeProvider();
        var cache = source.Cache(Interval, time);
        var slow = cache.Get("pt", TestContext.Current.CancellationToken).AsTask();
        source.Revision = 2;
        time.Advance(Interval);
        var fresh = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Act
        pending.SetResult("pt@1");
        var slowValue = await slow;
        var after = await cache.Get("pt", TestContext.Current.CancellationToken);

        // Assert
        slowValue.ShouldBe("pt@1");
        fresh.ShouldBe("pt@2");
        after.ShouldBe("pt@2");
        source.Loads.ShouldBe([("pt", 1L), ("pt", 2L)]);
    }

    [Fact]
    public async Task The_cancellation_token_reaches_the_revision_read()
    {
        // Arrange
        var source = new RevisionSource { Revision = 1 };
        var cache = source.Cache(Interval, new FakeTimeProvider());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> act = async () => await cache.Get("pt", cancellation.Token);

        // Assert
        await act.ShouldThrow<OperationCanceledException>();
        source.Loads.ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        // Arrange
        var source = new RevisionSource();
        var time = new FakeTimeProvider();

        // Act
        Action noRevision = () => _ = new RevisionCache<string, string>(null!, source.Load, Interval, time);
        Action noLoad = () => _ = new RevisionCache<string, string>(source.ReadRevision, null!, Interval, time);
        Action negative = () => _ = new RevisionCache<string, string>(source.ReadRevision, source.Load, TimeSpan.FromTicks(-1), time);
        Action noTime = () => _ = new RevisionCache<string, string>(source.ReadRevision, source.Load, Interval, null!);

        // Assert
        noRevision.ShouldThrow<ArgumentNullException>();
        noLoad.ShouldThrow<ArgumentNullException>();
        negative.ShouldThrow<ArgumentOutOfRangeException>();
        noTime.ShouldThrow<ArgumentNullException>();
    }
}
