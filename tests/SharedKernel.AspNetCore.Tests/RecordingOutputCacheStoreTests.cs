namespace PandaBear.AspNetCore.Tests;

public sealed class RecordingOutputCacheStoreTests
{
    [Fact]
    public async Task Eviction_records_tag_and_cancellation_before_throwing()
    {
        var expected = new InvalidOperationException("eviction failed");
        var store = new RecordingOutputCacheStore { EvictionException = expected };
        using var cancellation = new CancellationTokenSource();
        Func<Task> evict = () => store.EvictByTagAsync("catalog", cancellation.Token).AsTask();
        var exception = await evict.ShouldThrow<InvalidOperationException>();
        exception.ShouldBeSameAs(expected);
        store.EvictionObserved.ShouldBeTrue();
        store.EvictionTag.ShouldBe("catalog");
        store.EvictionCancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Reads_miss_and_writes_do_not_store_content()
    {
        var store = new RecordingOutputCacheStore();
        await store.SetAsync("key", [1], ["tag"], TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var value = await store.GetAsync("key", TestContext.Current.CancellationToken);
        value.ShouldBeNull();
        store.EvictionObserved.ShouldBeFalse();
        await store.EvictByTagAsync("tag", TestContext.Current.CancellationToken);
        store.EvictionTag.ShouldBe("tag");
    }
}
