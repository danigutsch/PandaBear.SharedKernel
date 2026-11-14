using Microsoft.Extensions.Time.Testing;
using PandaBear.ObjectStorage;

namespace PandaBear.Testing.ObjectStorage.Tests;

/// <summary>Tests that the in-memory object store keeps the <see cref="IObjectStore" /> contract of the S3 provider.</summary>
public sealed class InMemoryObjectStoreStorageTests
{
    [Fact]
    public static async Task OpenRead_returns_the_bytes_content_type_length_and_checksum_that_were_put()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        byte[] content = [1, 2, 3, 4];
        await store.Put(new ObjectWriteRequest("media/a.jpg", new MemoryStream(content), "image/jpeg", content.Length, "sha256:abc"), TestContext.Current.CancellationToken);

        // Act
        using var result = await store.OpenRead("media/a.jpg", TestContext.Current.CancellationToken);

        // Assert
        result.ObjectKey.ShouldBe("media/a.jpg");
        result.ContentType.ShouldBe("image/jpeg");
        result.Length.ShouldBe(4);
        result.Checksum.ShouldBe("sha256:abc");
        var bytes = await StreamBytes.ReadAll(result.Content);
        bytes.ShouldBe(content);
    }

    [Fact]
    public static async Task Put_returns_storage_and_public_uris_with_escaped_segments_under_the_configured_bases()
    {
        // Arrange
        var store = new InMemoryObjectStore
        {
            StorageBaseUri = new Uri("https://storage.example/bucket"),
            PublicBaseUri = new Uri("https://cdn.example/")
        };

        // Act
        var result = await store.Put(new ObjectWriteRequest("media/a b.jpg", new MemoryStream([1]), "image/jpeg", 1), TestContext.Current.CancellationToken);

        // Assert
        result.ObjectKey.ShouldBe("media/a b.jpg");
        result.StorageUri.ShouldBe(new Uri("https://storage.example/bucket/media/a%20b.jpg"));
        result.PublicUri.ShouldBe(new Uri("https://cdn.example/media/a%20b.jpg"));
        result.ContentType.ShouldBe("image/jpeg");
        result.Length.ShouldBe(1);
        result.Checksum.ShouldBeNull();
        store.GetPublicUri("media/a b.jpg").ShouldBe(result.PublicUri);
    }

    [Fact]
    public static void GetPublicUri_supports_a_relative_base()
    {
        // Arrange
        var store = new InMemoryObjectStore { PublicBaseUri = new Uri("/media", UriKind.Relative) };

        // Act
        var uri = store.GetPublicUri("v1/a.jpg");

        // Assert
        uri.IsAbsoluteUri.ShouldBeFalse();
        uri.OriginalString.ShouldBe("/media/v1/a.jpg");
    }

    [Fact]
    public static async Task OpenRead_of_a_missing_key_throws_file_not_found_naming_the_key()
    {
        // Arrange
        var store = new InMemoryObjectStore();

        // Act
        Func<Task> act = () => store.OpenRead("media/missing.jpg", TestContext.Current.CancellationToken).AsTask();

        // Assert
        var exception = await act.ShouldThrow<FileNotFoundException>();
        exception.FileName.ShouldBe("media/missing.jpg");
    }

    [Fact]
    public static async Task Exists_and_delete_follow_the_stored_objects_and_deleting_a_missing_key_completes()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var existedBefore = await store.Exists("media/a.jpg", ct);
        await store.Delete("media/a.jpg", ct);
        await store.Delete("media/never.jpg", ct);
        var existsAfter = await store.Exists("media/a.jpg", ct);

        // Assert
        existedBefore.ShouldBeTrue();
        existsAfter.ShouldBeFalse();
        store.ObjectKeys.ShouldBeEmpty();
    }

    [Fact]
    public static async Task ListKeys_returns_keys_starting_with_the_exact_prefix_in_ordinal_order()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/b.jpg", [1], "image/jpeg");
        store.Seed("media/A.jpg", [1], "image/jpeg");
        store.Seed("media-other/c.jpg", [1], "image/jpeg");
        store.Seed("other/d.jpg", [1], "image/jpeg");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var folder = await store.ListKeys("media/", ct);
        var partial = await store.ListKeys("media", ct);
        var all = await store.ListKeys(string.Empty, ct);

        // Assert
        folder.ShouldBe(["media/A.jpg", "media/b.jpg"]);
        partial.ShouldBe(["media-other/c.jpg", "media/A.jpg", "media/b.jpg"]);
        all.ShouldBe(["media-other/c.jpg", "media/A.jpg", "media/b.jpg", "other/d.jpg"]);
    }

    [Fact]
    public static async Task ListObjects_reports_the_time_of_the_write_and_overridden_modification_times()
    {
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var store = new InMemoryObjectStore { TimeProvider = time };
        await store.Put(new ObjectWriteRequest("media/new.jpg", new MemoryStream([1]), "image/jpeg", 1), TestContext.Current.CancellationToken);
        store.Seed("media/old.jpg", [1], "image/jpeg");
        store.SetLastModified("media/old.jpg", time.GetUtcNow().AddDays(-2));

        // Act
        var objects = await store.ListObjects("media/", TestContext.Current.CancellationToken);

        // Assert
        objects.ShouldBe(
        [
            new ObjectInventoryItem("media/new.jpg", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)),
            new ObjectInventoryItem("media/old.jpg", new DateTimeOffset(2025, 12, 31, 3, 4, 5, TimeSpan.Zero))
        ]);
    }

    [Theory]
    [InlineData("/rooted.jpg")]
    [InlineData("media/../escape.jpg")]
    [InlineData("media//empty.jpg")]
    [InlineData("")]
    public static async Task Key_operations_reject_keys_that_are_not_relative_slash_delimited_paths(string objectKey)
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var ct = TestContext.Current.CancellationToken;

        // Act
        Func<Task> put = () => store.Put(new ObjectWriteRequest(objectKey, new MemoryStream([1]), "image/jpeg", 1), ct).AsTask();
        Func<Task> read = () => store.OpenRead(objectKey, ct).AsTask();
        Func<Task> exists = () => store.Exists(objectKey, ct).AsTask();
        Func<Task> delete = () => store.Delete(objectKey, ct).AsTask();
        Action publicUri = () => store.GetPublicUri(objectKey);

        // Assert
        (await put.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("objectKey");
        (await read.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("objectKey");
        (await exists.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("objectKey");
        (await delete.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("objectKey");
        publicUri.ShouldThrow<ArgumentException>().ParamName.ShouldBe("objectKey");
        store.Calls.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Key_operations_enforce_the_configured_maximum_key_length()
    {
        // Arrange
        var store = new InMemoryObjectStore { MaxObjectKeyLength = 5 };

        // Act
        Func<Task> act = () => store.Exists("media/a.jpg", TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrow<ArgumentException>();
        var shortKeyExists = await store.Exists("a.jpg", TestContext.Current.CancellationToken);
        shortKeyExists.ShouldBeFalse();
    }

    [Fact]
    public static async Task Listing_rejects_a_prefix_with_dot_segments_and_treats_null_as_empty()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");

        // Act
        Func<Task> act = () => store.ListKeys("../media/", TestContext.Current.CancellationToken).AsTask();
        var all = await store.ListKeys(null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("prefix");
        all.ShouldBe(["media/a.jpg"]);
    }

    [Fact]
    public static async Task Put_rejects_content_whose_length_differs_from_the_declared_length_and_stores_nothing()
    {
        // Arrange
        var store = new InMemoryObjectStore();

        // Act
        Func<Task> act = () => store.Put(new ObjectWriteRequest("media/a.jpg", new MemoryStream([1, 2, 3]), "image/jpeg", 2), TestContext.Current.CancellationToken).AsTask();

        // Assert
        (await act.ShouldThrow<ArgumentException>()).ParamName.ShouldBe("request");
        store.ObjectKeys.ShouldBeEmpty();
        store.Puts.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Stored_objects_keep_a_copy_of_the_content_with_metadata_and_checksum()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        byte[] content = [1, 2, 3];
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "tests" };
        await store.Put(new ObjectWriteRequest("media/a.jpg", new MemoryStream(content), "image/jpeg", 3, "sha256:abc", metadata), TestContext.Current.CancellationToken);

        // Act
        content[0] = 9;
        metadata["owner"] = "changed";
        var stored = store.FindObject("media/a.jpg");

        // Assert
        var found = stored.ShouldNotBeNull();
        found.Content.ToArray().ShouldBe([1, 2, 3]);
        found.Length.ShouldBe(3);
        found.ContentType.ShouldBe("image/jpeg");
        found.Checksum.ShouldBe("sha256:abc");
        found.Metadata["owner"].ShouldBe("tests");
        store.FindObject("media/missing.jpg").ShouldBeNull();
    }

    [Fact]
    public static async Task OpenRead_hands_out_an_independent_read_only_stream_per_call()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1, 2], "image/jpeg");
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var first = await store.OpenRead("media/a.jpg", ct);
        _ = await StreamBytes.ReadAll(first.Content);
        using var second = await store.OpenRead("media/a.jpg", ct);

        // Assert
        first.Content.CanWrite.ShouldBeFalse();
        var secondBytes = await StreamBytes.ReadAll(second.Content);
        secondBytes.ShouldBe([1, 2]);
    }

    [Fact]
    public static async Task Operations_observe_a_cancelled_token_before_doing_anything()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        store.Seed("media/a.jpg", [1], "image/jpeg");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> delete = () => store.Delete("media/a.jpg", cancellation.Token).AsTask();

        // Assert
        await delete.ShouldThrowAssignableTo<OperationCanceledException>();
        store.ObjectKeys.ShouldBe(["media/a.jpg"]);
        store.Calls.ShouldBeEmpty();
    }

    [Fact]
    public static void Seed_stores_an_object_without_recording_a_call()
    {
        // Arrange
        var store = new InMemoryObjectStore();

        // Act
        store.Seed("media/a.jpg", [1, 2], "image/jpeg", "sha256:abc");

        // Assert
        store.ObjectKeys.ShouldBe(["media/a.jpg"]);
        store.FindObject("media/a.jpg").ShouldNotBeNull().Checksum.ShouldBe("sha256:abc");
        store.Calls.ShouldBeEmpty();
        store.Puts.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Concurrent_puts_of_distinct_keys_are_all_stored_and_recorded()
    {
        // Arrange
        var store = new InMemoryObjectStore();
        var keys = Enumerable.Range(0, 200).Select(static index => $"media/{index:D3}.bin").ToArray();

        // Act
        await Parallel.ForEachAsync(keys, TestContext.Current.CancellationToken, async (key, ct) =>
            await store.Put(new ObjectWriteRequest(key, new MemoryStream([1, 2]), "application/octet-stream", 2), ct));

        // Assert
        store.ObjectKeys.ShouldBe(keys);
        store.Puts.Count.ShouldBe(200);
        store.CallCount(ObjectStoreOperation.Put).ShouldBe(200);
    }
}
