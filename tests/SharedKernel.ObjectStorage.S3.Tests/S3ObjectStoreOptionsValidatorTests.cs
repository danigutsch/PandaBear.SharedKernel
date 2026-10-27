
namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Tests that S3 object store options validation rejects each invalid setting.</summary>
public sealed class S3ObjectStoreOptionsValidatorTests
{
    [Theory]
    [InlineData("endpoint")]
    [InlineData("relative-endpoint")]
    [InlineData("unsupported-scheme")]
    [InlineData("bucket")]
    [InlineData("access-key")]
    [InlineData("secret-key")]
    [InlineData("timeout")]
    [InlineData("public-base-uri")]
    [InlineData("authentication-region")]
    [InlineData("max-object-key-length")]
    public void Validate_fails_for_invalid_required_storage_configuration(string invalidField)
    {
        // Arrange
        var options = S3ObjectStoreOptionsTestFactory.CreateValidOptions();
        switch (invalidField)
        {
            case "endpoint":
                options.Endpoint = null;
                break;
            case "relative-endpoint":
                options.Endpoint = new Uri("seaweedfs", UriKind.Relative);
                break;
            case "unsupported-scheme":
                options.Endpoint = new Uri("ftp://seaweedfs:8333");
                break;
            case "bucket":
                options.Bucket = " ";
                break;
            case "access-key":
                options.AccessKey = " ";
                break;
            case "secret-key":
                options.SecretKey = " ";
                break;
            case "timeout":
                options.BucketProvisioningTimeout = TimeSpan.Zero;
                break;
            case "public-base-uri":
                options.PublicBaseUri = null!;
                break;
            case "authentication-region":
                options.AuthenticationRegion = " ";
                break;
            case "max-object-key-length":
                options.MaxObjectKeyLength = 0;
                break;
        }

        var validator = new S3ObjectStoreOptionsValidator();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBe(false);
    }

    [Fact]
    public void Validate_succeeds_for_complete_storage_configuration()
    {
        // Arrange
        var validator = new S3ObjectStoreOptionsValidator();

        // Act
        var result = validator.Validate(null, S3ObjectStoreOptionsTestFactory.CreateValidOptions());

        // Assert
        result.Succeeded.ShouldBe(true);
    }

    [Fact]
    public void Validate_fails_when_bucket_provisioning_timeout_exceeds_the_cancellation_token_limit()
    {
        // Arrange
        var options = S3ObjectStoreOptionsTestFactory.CreateValidOptions();
        options.BucketProvisioningTimeout = TimeSpan.MaxValue;
        var validator = new S3ObjectStoreOptionsValidator();

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBe(false);
    }
}
