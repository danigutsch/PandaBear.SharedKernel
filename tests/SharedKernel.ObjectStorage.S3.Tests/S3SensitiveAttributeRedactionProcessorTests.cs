using System.Diagnostics;

namespace PandaBear.ObjectStorage.S3.Tests;

public sealed class S3SensitiveAttributeRedactionProcessorTests
{
    [Fact]
    public void OnEnd_removes_sensitive_identifiers_from_s3_activity()
    {
        // Arrange
        using var activity = new Activity("S3.PutObject");
        activity.Start();
        activity.SetTag("aws.s3.bucket", "media-private");
        activity.SetTag("aws.s3.key", "media/customer/image.jpg");
        activity.SetTag("aws.s3.copy_source", "private/source.jpg");
        activity.SetTag("aws.s3.delete", "private/delete.jpg");
        activity.SetTag("aws.s3.upload_id", "upload-id");
        activity.SetTag("aws.s3.part_number", 1);
        activity.SetTag("aws.request_id", "request-id");
        activity.SetTag("aws.extended_request_id", "extended-request-id");
        activity.SetTag("url.full", "https://seaweedfs/media/private/image.jpg");
        activity.SetTag("http.url", "https://seaweedfs/media/private/image.jpg");
        using var processor = new S3SensitiveAttributeRedactionProcessor();

        // Act
        processor.OnEnd(activity);

        // Assert
        activity.GetTagItem("aws.s3.bucket").ShouldBeNull();
        activity.GetTagItem("aws.s3.key").ShouldBeNull();
        activity.GetTagItem("aws.s3.copy_source").ShouldBeNull();
        activity.GetTagItem("aws.s3.delete").ShouldBeNull();
        activity.GetTagItem("aws.s3.upload_id").ShouldBeNull();
        activity.GetTagItem("aws.s3.part_number").ShouldBeNull();
        activity.GetTagItem("aws.request_id").ShouldBeNull();
        activity.GetTagItem("aws.extended_request_id").ShouldBeNull();
        activity.GetTagItem("url.full").ShouldBeNull();
        activity.GetTagItem("http.url").ShouldBeNull();
    }

    [Fact]
    public void OnEnd_leaves_non_s3_activity_attributes_unchanged()
    {
        // Arrange
        using var activity = new Activity("HTTP GET");
        activity.Start();
        activity.SetTag("url.full", "https://example.test/public");
        using var processor = new S3SensitiveAttributeRedactionProcessor();

        // Act
        processor.OnEnd(activity);

        // Assert
        activity.GetTagItem("url.full").ShouldBe("https://example.test/public");
    }

    [Fact]
    public void OnEnd_removes_request_identifiers_from_bucketless_aws_activity()
    {
        // Arrange
        using var activity = new Activity("S3.CredentialsRetrieval");
        activity.Start();
        activity.SetTag("aws.request_id", "request-id");
        activity.SetTag("aws.extended_request_id", "extended-request-id");
        activity.SetTag("url.full", "https://seaweedfs/private");
        using var processor = new S3SensitiveAttributeRedactionProcessor();

        // Act
        processor.OnEnd(activity);

        // Assert
        activity.GetTagItem("aws.request_id").ShouldBeNull();
        activity.GetTagItem("aws.extended_request_id").ShouldBeNull();
        activity.GetTagItem("url.full").ShouldBeNull();
    }
}
