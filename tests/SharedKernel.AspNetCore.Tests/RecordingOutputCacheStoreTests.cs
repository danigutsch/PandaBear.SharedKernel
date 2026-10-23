namespace PandaBear.AspNetCore.Tests;

public sealed class RecordingOutputCacheStoreTests
{
    [Fact]
    public async Task Eviction_records_tag_and_cancellation_before_throwing()
    {
        // Arrange
        var expected = new InvalidOperationException("eviction failed");
        var store = new RecordingOutputCacheStore { EvictionException = expected };
        using var cancellation = new CancellationTokenSource();
        Func<Task> evict = () => store.EvictByTagAsync("catalog", cancellation.Token).AsTask();

        // Act
        var exception = await evict.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.ShouldBeSameAs(expected);
        store.EvictionObserved.ShouldBeTrue();
        store.EvictionTag.ShouldBe("catalog");
        store.EvictionCancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Reads_miss_and_writes_do_not_store_content()
    {
        // Arrange
        var store = new RecordingOutputCacheStore();

        // Act
        await store.SetAsync("key", [1], ["tag"], TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var value = await store.GetAsync("key", TestContext.Current.CancellationToken);
        var evictionObservedBeforeEviction = store.EvictionObserved;
        await store.EvictByTagAsync("tag", TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBeNull();
        evictionObservedBeforeEviction.ShouldBeFalse();
        store.EvictionTag.ShouldBe("tag");
    }
}
