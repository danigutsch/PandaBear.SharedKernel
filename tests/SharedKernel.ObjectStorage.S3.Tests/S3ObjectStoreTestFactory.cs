using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3.Tests;

internal static class S3ObjectStoreTestFactory
{
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
}
