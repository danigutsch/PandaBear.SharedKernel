using System.Diagnostics;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests the activities that S3 object store operations emit.</summary>
public sealed class S3ObjectStoreTelemetryTests
{
    [Fact]
    public async Task Operations_emit_activities_without_bucket_key_or_url_tags()
    {
        // Arrange
        var expectedSource = S3ObjectStoreTelemetry.ActivitySource;
        var activities = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, expectedSource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        _ = await store.Exists("private/customer/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        listener.Dispose();
        // Other tests share the source, so assert over every activity seen.
        activities.Any(static activity => activity.OperationName == "object_storage.exists").ShouldBe(true);
        activities.SelectMany(static activity => activity.TagObjects)
            .Any(static tag => tag.Key != "object_storage.operation" || $"{tag.Value}".Contains("customer", StringComparison.Ordinal))
            .ShouldBe(false);
    }

    [Fact]
    public async Task Operations_suppress_downstream_http_instrumentation_and_restore_it()
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        _ = await store.Exists("private/customer/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        client.SuppressedDuringMetadataLookup.ShouldBe(true);
        OpenTelemetry.Sdk.SuppressInstrumentation.ShouldBe(false);
    }
}
