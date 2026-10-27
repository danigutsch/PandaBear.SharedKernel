using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Creates S3 object stores over a fake client for tests.</summary>
internal static class S3ObjectStoreTestFactory
{
    /// <summary>Creates a store over the fake client.</summary>
    public static S3ObjectStore CreateStore(FakeAmazonS3Client client, bool autoProvisionBucket = true, TimeProvider? timeProvider = null)
    {
        return new S3ObjectStore(client, Options.Create(new S3ObjectStoreOptions
        {
            Endpoint = new Uri("http://seaweedfs:8333/"),
            PublicBaseUri = new Uri("/cdn/", UriKind.Relative),
            Bucket = "media",
            AccessKey = "access",
            SecretKey = "secret",
            AutoProvisionBucket = autoProvisionBucket
        }), timeProvider ?? TimeProvider.System);
    }

    /// <summary>Creates a health check over the fake client.</summary>
    public static S3ObjectStoreHealthCheck CreateHealthCheck(FakeAmazonS3Client client) =>
        new(client, Options.Create(S3ObjectStoreOptionsTestFactory.CreateValidOptions()));
}
