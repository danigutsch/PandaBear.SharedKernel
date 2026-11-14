using PandaBear.ObjectStorage;

namespace PandaBear.Testing.ObjectStorage.Tests;

/// <summary>Tests the calls, writes, reads and deletes that the in-memory object store records.</summary>
public sealed class InMemoryObjectStoreRecordingTests
{
    [Fact]
    public static async Task Puts_record_how_the_content_stream_looked_when_the_store_received_it()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "tests" };
        using var content = new MemoryStream([1, 2, 3], writable: false);

        // Act
        await store.Put(new ObjectWriteRequest("media/a.jpg", content, "image/jpeg", 3, "sha256:abc", metadata), TestContext.Current.CancellationToken);

        // Assert
        var put = store.Puts.ShouldHaveSingleItem();
        put.ObjectKey.ShouldBe("media/a.jpg");
        put.ContentType.ShouldBe("image/jpeg");
        put.DeclaredLength.ShouldBe(3);
        put.Checksum.ShouldBe("sha256:abc");
        put.Metadata.ShouldNotBeNull()["owner"].ShouldBe("tests");
        put.StartPosition.ShouldBe(0);
        put.ContentCanWrite.ShouldBeFalse();
        put.ContentStreamType.ShouldBe(typeof(MemoryStream));
        put.BytesRead.ShouldBe(3);
        put.Content.ToArray().ShouldBe([1, 2, 3]);
    }

    [Fact]
    public static async Task Puts_record_the_start_position_and_only_the_bytes_read_from_there()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        using var content = new MemoryStream([9, 1, 2]);
        content.Position = 1;

        // Act
        await store.Put(new ObjectWriteRequest("media/a.jpg", content, "image/jpeg", 2), TestContext.Current.CancellationToken);

        // Assert
        var put = store.Puts.ShouldHaveSingleItem();
        put.StartPosition.ShouldBe(1);
        put.ContentCanWrite.ShouldBeTrue();
        put.BytesRead.ShouldBe(2);
        put.Content.ToArray().ShouldBe([1, 2]);
    }

    [Fact]
    public static async Task Puts_record_no_start_position_for_a_stream_that_cannot_seek()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        using var content = new NonSeekableStream([1, 2]);

        // Act
        await store.Put(new ObjectWriteRequest("media/a.jpg", content, "image/jpeg", 2), TestContext.Current.CancellationToken);

        // Assert
        var put = store.Puts.ShouldHaveSingleItem();
        put.StartPosition.ShouldBeNull();
        put.ContentStreamType.ShouldBe(typeof(NonSeekableStream));
        put.Content.ToArray().ShouldBe([1, 2]);
    }

    [Fact]
    public static async Task Calls_record_every_operation_in_order_with_its_key_or_prefix()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await store.Put(new ObjectWriteRequest("media/a.jpg", new MemoryStream([1]), "image/jpeg", 1), ct);
        _ = await store.Exists("media/a.jpg", ct);
        using (await store.OpenRead("media/a.jpg", ct))
        {
        }

        _ = await store.ListKeys("media/", ct);
        _ = await store.ListObjects(string.Empty, ct);
        _ = store.GetPublicUri("media/a.jpg");
        _ = await store.CreateUploadUrl(new ObjectUploadRequest("media/b.jpg", "image/jpeg", 1, TimeSpan.FromMinutes(5)), ct);
        await store.Delete("media/a.jpg", ct);

        // Assert
        store.Calls.ShouldBe(
        [
            new ObjectStoreCall(ObjectStoreOperation.Put, "media/a.jpg"),
            new ObjectStoreCall(ObjectStoreOperation.Exists, "media/a.jpg"),
            new ObjectStoreCall(ObjectStoreOperation.OpenRead, "media/a.jpg"),
            new ObjectStoreCall(ObjectStoreOperation.ListKeys, "media/"),
            new ObjectStoreCall(ObjectStoreOperation.ListObjects, string.Empty),
            new ObjectStoreCall(ObjectStoreOperation.GetPublicUri, "media/a.jpg"),
            new ObjectStoreCall(ObjectStoreOperation.CreateUploadUrl, "media/b.jpg"),
            new ObjectStoreCall(ObjectStoreOperation.Delete, "media/a.jpg")
        ]);
        store.CallCount(ObjectStoreOperation.Exists).ShouldBe(1);
        store.CallCount(ObjectStoreOperation.Put).ShouldBe(1);
    }

    [Fact]
    public static async Task Reads_and_deletes_record_completed_operations()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var read = await store.OpenRead("media/a.jpg", ct);
        Func<Task> missingRead = () => store.OpenRead("media/missing.jpg", ct).AsTask();
        await missingRead.ShouldThrow<FileNotFoundException>();
        await store.Delete("media/a.jpg", ct);
        await store.Delete("media/missing.jpg", ct);

        // Assert
        store.Reads.ShouldHaveSingleItem().ShouldBeSameAs(read);
        store.Deletes.ShouldBe(["media/a.jpg", "media/missing.jpg"]);
        store.CallCount(ObjectStoreOperation.OpenRead).ShouldBe(2);
    }

    [Fact]
    public static async Task Wrapped_read_content_replaces_the_stream_handed_out_by_open_read()
    {
        // Arrange
        var wrappedKeys = new List<string>();
        var store = new InMemoryObjectStore
        {
            WrapReadContent = (objectKey, content) =>
            {
                wrappedKeys.Add(objectKey);
                return new NonSeekableStream(content);
            }
        };
        store.Seed("media/a.jpg", [1, 2], "image/jpeg");

        // Act
        using var result = await store.OpenRead("media/a.jpg", TestContext.Current.CancellationToken);

        // Assert
        result.Content.ShouldBeOfType<NonSeekableStream>();
        result.Length.ShouldBe(2);
        wrappedKeys.ShouldBe(["media/a.jpg"]);
        store.Reads.ShouldHaveSingleItem().Content.ShouldBeSameAs(result.Content);
    }

    [Fact]
    public static async Task Recorded_collections_are_snapshots_that_later_calls_do_not_change()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var ct = TestContext.Current.CancellationToken;
        await store.Put(new ObjectWriteRequest("media/a.jpg", new MemoryStream([1]), "image/jpeg", 1), ct);
        var calls = store.Calls;
        var keys = store.ObjectKeys;

        // Act
        await store.Put(new ObjectWriteRequest("media/b.jpg", new MemoryStream([1]), "image/jpeg", 1), ct);

        // Assert
        calls.Count.ShouldBe(1);
        keys.ShouldBe(["media/a.jpg"]);
        store.ObjectKeys.ShouldBe(["media/a.jpg", "media/b.jpg"]);
    }
}
