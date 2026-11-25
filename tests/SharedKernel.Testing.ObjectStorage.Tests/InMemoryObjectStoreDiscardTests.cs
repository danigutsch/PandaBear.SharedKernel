using SharedKernel.ObjectStorage;

namespace SharedKernel.Testing.ObjectStorage.Tests;

/// <summary>Tests the write-discarding mode of the in-memory object store.</summary>
public sealed class InMemoryObjectStoreDiscardTests
{
    [Fact]
    public static async Task DiscardWrites_drains_the_content_records_its_size_and_stores_nothing()
    {
        // Arrange
        var store = new InMemoryObjectStore { DiscardWrites = true };
        using var content = new MemoryStream([1, 2, 3]);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await store.Put(new ObjectWriteRequest("media/a.jpg", content, "image/jpeg", 3, "sha256:abc"), ct);

        // Assert
        result.Length.ShouldBe(3);
        result.Checksum.ShouldBe("sha256:abc");
        content.Position.ShouldBe(3);
        var put = store.Puts.ShouldHaveSingleItem();
        put.StartPosition.ShouldBe(0);
        put.BytesRead.ShouldBe(3);
        put.DeclaredLength.ShouldBe(3);
        put.Content.IsEmpty.ShouldBeTrue();
        store.ObjectKeys.ShouldBeEmpty();
        var exists = await store.Exists("media/a.jpg", ct);
        exists.ShouldBeFalse();
    }

    [Fact]
    public static async Task DiscardWrites_does_not_allocate_a_payload_sized_buffer()
    {
        // Arrange
        const int payloadBytes = 4 * 1024 * 1024;
        var payload = new byte[payloadBytes];
        var store = new InMemoryObjectStore { DiscardWrites = true };
        var ct = TestContext.Current.CancellationToken;
        await store.Put(new ObjectWriteRequest("media/warm-up.bin", new MemoryStream(payload), "application/octet-stream", payloadBytes), ct);
        using var measured = new MemoryStream(payload);
        var request = new ObjectWriteRequest("media/measured.bin", measured, "application/octet-stream", payloadBytes);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        await store.Put(request, ct);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        store.Puts[^1].BytesRead.ShouldBe(payloadBytes);
        allocated.ShouldBeLessThan(payloadBytes / 4);
    }
}
