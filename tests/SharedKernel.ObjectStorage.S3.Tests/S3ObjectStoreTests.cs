using Amazon.S3.Model;
using Amazon.S3;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests S3 object store reads, writes, listing, and bucket provisioning against a fake client.</summary>
public sealed class S3ObjectStoreTests
{
    [Fact]
    public async Task OpenRead_initializes_the_bucket_before_reading_an_object()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        var result = await store.OpenRead("media/image.jpg", TestContext.Current.CancellationToken);
        await result.Content.DisposeAsync();

        // Assert
        client.Operations.ShouldBe(["PutBucket:media", "GetObject:media/media/image.jpg"]);
        result.Checksum.ShouldBe("sha256:abc");
    }

    [Fact]
    public async Task OpenRead_throws_file_not_found_when_object_is_not_found()
    {
        // Arrange
        using var client = new FakeAmazonS3Client
        {
            GetObjectException = new AmazonS3Exception("Not found")
            {
                StatusCode = System.Net.HttpStatusCode.NotFound
            }
        };
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        Func<Task> act = () => store.OpenRead("media/image.jpg", TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrow<FileNotFoundException>();
        client.Operations.ShouldBe(["GetObject:media/media/image.jpg"]);
    }

    [Fact]
    public async Task Exists_initializes_the_bucket_before_metadata_lookup()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        var exists = await store.Exists("media/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBe(true);
        client.Operations.ShouldBe(["PutBucket:media", "GetObjectMetadata:media/media/image.jpg"]);
    }

    [Fact]
    public async Task Exists_does_not_require_bucket_create_permission_when_auto_provisioning_is_disabled()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        var exists = await store.Exists("media/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBe(true);
        client.Operations.ShouldBe(["GetObjectMetadata:media/media/image.jpg"]);
    }

    [Fact]
    public async Task Exists_returns_false_when_object_metadata_is_not_found()
    {
        // Arrange
        using var client = new FakeAmazonS3Client
        {
            GetObjectMetadataException = new AmazonS3Exception("Not found")
            {
                StatusCode = System.Net.HttpStatusCode.NotFound
            }
        };
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        var exists = await store.Exists("media/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBe(false);
        client.Operations.ShouldBe(["GetObjectMetadata:media/media/image.jpg"]);
    }

    [Fact]
    public async Task Exists_reuses_inflight_bucket_initialization_after_a_caller_cancels()
    {
        // Arrange
        var bucketCreated = new TaskCompletionSource<PutBucketResponse>();
        using var client = new FakeAmazonS3Client
        {
            PutBucketTask = bucketCreated.Task
        };
        var store = S3ObjectStoreTestFactory.CreateStore(client);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> act = () => store.Exists("media/image.jpg", cancellation.Token).AsTask();
        await act.ShouldThrowAssignableTo<OperationCanceledException>();
        bucketCreated.SetResult(new PutBucketResponse());
        var exists = await store.Exists("media/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        exists.ShouldBe(true);
        client.Operations.ShouldBe([
            "PutBucket:media",
            "GetObjectMetadata:media/media/image.jpg"]);
    }

    [Fact]
    public async Task Delete_initializes_the_bucket_before_deleting_an_object()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        await store.Delete("media/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        client.Operations.ShouldBe(["PutBucket:media", "DeleteObject:media/media/image.jpg"]);
    }

    [Fact]
    public async Task ListKeys_initializes_the_bucket_once_and_reads_all_pages()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        var keys = await store.ListKeys("media", TestContext.Current.CancellationToken);

        // Assert
        keys.ShouldBe(["media/one.jpg", "media/two.jpg"]);
        client.Operations.ShouldBe([
            "PutBucket:media",
            "ListObjectsV2:media/media/",
            "ListObjectsV2:media/media/next"]);
    }

    [Fact]
    public async Task ListObjects_treats_unspecified_s3_timestamps_as_utc()
    {
        // Arrange
        var unspecifiedTimestamp = new DateTime(2026, 7, 12, 12, 30, 0, DateTimeKind.Unspecified);
        using var client = new FakeAmazonS3Client { ListObjectLastModified = unspecifiedTimestamp };
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        var objects = await store.ListObjects("media", TestContext.Current.CancellationToken);

        // Assert
        objects.Count.ShouldBe(2);
        objects[0].LastModifiedAt.ShouldBe(new DateTimeOffset(2026, 7, 12, 12, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ListObjects_treats_a_missing_object_collection_as_empty()
    {
        // Arrange
        using var client = new FakeAmazonS3Client { ReturnEmptyListResponse = true };
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        var objects = await store.ListObjects("media", TestContext.Current.CancellationToken);

        // Assert
        objects.ShouldBeEmpty();
        client.Operations.ShouldBe(["ListObjectsV2:media/media/"]);
    }

    [Fact]
    public async Task Put_stores_checksum_metadata_and_returns_escaped_public_uri()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);
        var request = new ObjectWriteRequest("media/folder/image 1.jpg", new MemoryStream("image"u8.ToArray()), "image/jpeg", 5, "sha256:abc");

        // Act
        var result = await store.Put(request, TestContext.Current.CancellationToken);

        // Assert
        client.LastPutObjectRequest.ShouldNotBeNull();
        client.LastPutObjectRequest.Metadata["checksum"].ShouldBe("sha256:abc");
        result.PublicUri.ToString().ShouldBe("/cdn/media/folder/image%201.jpg");
    }

    [Fact]
    public async Task Put_uses_explicit_checksum_when_request_metadata_contains_a_checksum()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);
        var request = new ObjectWriteRequest(
            "media/image.jpg",
            new MemoryStream("image"u8.ToArray()),
            "image/jpeg",
            5,
            "sha256:explicit",
            new Dictionary<string, string> { ["checksum"] = "caller-value" });

        // Act
        await store.Put(request, TestContext.Current.CancellationToken);

        // Assert
        client.LastPutObjectRequest.ShouldNotBeNull();
        client.LastPutObjectRequest.Metadata["checksum"].ShouldBe("sha256:explicit");
    }

    [Fact]
    public void GetPublicUri_rejects_traversal_keys_before_network_access()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client);

        // Act
        var act = () => store.GetPublicUri("media/../secret.jpg");

        // Assert
        act.ShouldThrow<ArgumentException>();
        client.Operations.ShouldBe([]);
    }
}
