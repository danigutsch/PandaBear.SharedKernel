
namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Creates S3 object store options for tests.</summary>
internal static class S3ObjectStoreOptionsTestFactory
{
    /// <summary>Creates options that pass validation.</summary>
    public static S3ObjectStoreOptions CreateValidOptions()
    {
        return new S3ObjectStoreOptions
        {
            Endpoint = new Uri("https://seaweedfs.example"),
            PublicBaseUri = new Uri("https://cdn.example/media/"),
            Bucket = "media",
            AccessKey = "access",
            SecretKey = "secret",
            BucketProvisioningTimeout = TimeSpan.FromSeconds(30)
        };
    }
}
