using System.Diagnostics;

namespace PandaBear.ObjectStorage.S3.Tests;

public sealed class S3ObjectStoreTelemetryTests
{
    [Fact]
    public async Task Operations_emit_activities_without_bucket_key_or_url_tags()
    {
        // Arrange
        var expectedSource = S3ObjectStoreTelemetry.ActivitySource;
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, expectedSource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);

        // Act
        _ = await store.Exists("private/customer/image.jpg", TestContext.Current.CancellationToken);

        // Assert
        activities.Count.ShouldBe(1);
        var activity = activities[0];
        activity.TagObjects.Select(static tag => tag.Key).ShouldBe(["object_storage.operation"]);
        activity.TagObjects.Any(static tag => $"{tag.Value}".Contains("customer", StringComparison.Ordinal)).ShouldBe(false);
    }
}
