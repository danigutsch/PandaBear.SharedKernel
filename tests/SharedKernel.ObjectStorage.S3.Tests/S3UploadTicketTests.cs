using System.Globalization;
using System.Web;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests the signed upload tickets that the S3 object store creates.</summary>
public sealed class S3UploadTicketTests
{
    [Fact]
    public async Task Creates_upload_ticket_with_signed_content_type_and_length()
    {
        // Arrange
        using var client = new AmazonS3Client(new BasicAWSCredentials("test-access", "test-secret"),
            new AmazonS3Config { ServiceURL = "https://s3.eu-central-1.amazonaws.com", AuthenticationRegion = "eu-central-1", ForcePathStyle = true });
        var now = new DateTimeOffset(2026, 6, 22, 12, 30, 0, TimeSpan.Zero);
        var store = new S3ObjectStore(client, Options.Create(new S3ObjectStoreOptions
        {
            Endpoint = new Uri("https://s3.eu-central-1.amazonaws.com"),
            Bucket = "media",
            AccessKey = "test-access",
            SecretKey = "test-secret"
        }), new FakeTimeProvider(now));
        var request = new ObjectUploadRequest("pending/image.jpg", "image/jpeg", 123, TimeSpan.FromMinutes(5));

        // Act
        var ticket = await store.CreateUploadUrl(request, TestContext.Current.CancellationToken);

        // Assert
        ticket.ObjectKey.ShouldBe(request.ObjectKey);
        ticket.RequiredHeaders["Content-Type"].ShouldBe("image/jpeg");
        ticket.RequiredHeaders["Content-Length"].ShouldBe("123");
        var query = Uri.UnescapeDataString(ticket.UploadUri.Query);
        query.ShouldContain("X-Amz-SignedHeaders=content-length;content-type;host", StringComparison.Ordinal);
        ticket.ExpiresAt.ShouldBe(now.Add(request.ExpiresAfter));
        var parameters = HttpUtility.ParseQueryString(ticket.UploadUri.Query);
        var signedAtText = parameters["X-Amz-Date"];
        var lifetimeText = parameters["X-Amz-Expires"];
        signedAtText.ShouldNotBeNull();
        lifetimeText.ShouldNotBeNull();
        var signedAt = DateTimeOffset.ParseExact(signedAtText, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var signedLifetime = int.Parse(lifetimeText, CultureInfo.InvariantCulture);
        var signedExpiry = signedAt.AddSeconds(signedLifetime);
        // The SDK rounds the remaining lifetime and serializes the signing timestamp to whole seconds.
        var expiryDifference = (ticket.ExpiresAt - signedExpiry).Duration();
        expiryDifference.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task Preserves_configured_endpoint_scheme_in_upload_tickets(string scheme)
    {
        // Arrange
        var endpoint = new Uri($"{scheme}://storage.example.com");
        using var client = new AmazonS3Client(new BasicAWSCredentials("test-access", "test-secret"),
            new AmazonS3Config { ServiceURL = endpoint.AbsoluteUri, AuthenticationRegion = "us-east-1", ForcePathStyle = true });
        var store = new S3ObjectStore(client, Options.Create(new S3ObjectStoreOptions
        {
            Endpoint = endpoint,
            Bucket = "media",
            AccessKey = "test-access",
            SecretKey = "test-secret"
        }), TimeProvider.System);
        var request = new ObjectUploadRequest("pending/image.jpg", "image/jpeg", 123, TimeSpan.FromMinutes(5));

        // Act
        var ticket = await store.CreateUploadUrl(request, TestContext.Current.CancellationToken);

        // Assert
        ticket.UploadUri.Scheme.ShouldBe(scheme);
    }

    [Theory]
    [InlineData("../image.jpg", "image/jpeg", 1, 300)]
    [InlineData("pending/image.jpg", "", 1, 300)]
    [InlineData("pending/image.jpg", "image/jpeg\r\nx-test: value", 1, 300)]
    public async Task Rejects_invalid_upload_ticket_requests(string key, string contentType, long length, int seconds)
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);
        var request = new ObjectUploadRequest(key, contentType, length, TimeSpan.FromSeconds(seconds));

        // Act
        Func<Task> act = () => store.CreateUploadUrl(request, TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(1, 0)]
    [InlineData(1, 604801)]
    public async Task Rejects_invalid_upload_size_or_lifetime(long length, int seconds)
    {
        // Arrange
        using var client = new FakeAmazonS3Client();
        var store = S3ObjectStoreTestFactory.CreateStore(client, autoProvisionBucket: false);
        var request = new ObjectUploadRequest("pending/image.jpg", "image/jpeg", length, TimeSpan.FromSeconds(seconds));

        // Act
        Func<Task> act = () => store.CreateUploadUrl(request, TestContext.Current.CancellationToken).AsTask();

        // Assert
        await act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
