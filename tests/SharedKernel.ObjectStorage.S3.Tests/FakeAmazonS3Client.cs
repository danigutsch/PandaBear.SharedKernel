using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PandaBear.ObjectStorage.S3.Tests;

internal sealed class FakeAmazonS3Client : AmazonS3Client
{
    public FakeAmazonS3Client()
        : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost:8333", ForcePathStyle = true })
    {
    }

    public List<string> Operations { get; } = [];

    public PutObjectRequest? LastPutObjectRequest
    {
        get; private set;
    }

    public Task<PutBucketResponse>? PutBucketTask
    {
        get; set;
    }

    public Exception? GetObjectMetadataException
    {
        get; set;
    }

    public Exception? GetObjectException
    {
        get; set;
    }

    public DateTime ListObjectLastModified { get; set; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    public bool ReturnEmptyListResponse
    {
        get; set;
    }

    public override Task<PutBucketResponse> PutBucketAsync(PutBucketRequest request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"PutBucket:{request.BucketName}");
        if (PutBucketTask is not null)
        {
            return PutBucketTask.WaitAsync(cancellationToken);
        }

        return Task.FromResult(new PutBucketResponse());
    }

    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"PutObject:{request.BucketName}/{request.Key}");
        LastPutObjectRequest = request;
        return Task.FromResult(new PutObjectResponse());
    }

    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"GetObject:{request.BucketName}/{request.Key}");
        if (GetObjectException is not null)
        {
            throw GetObjectException;
        }

        var response = new GetObjectResponse
        {
            ResponseStream = new MemoryStream("image"u8.ToArray())
        };
        response.Headers.ContentType = "image/jpeg";
        response.Headers.ContentLength = 5;
        response.Metadata["checksum"] = "sha256:abc";
        return Task.FromResult(response);
    }

    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"GetObjectMetadata:{request.BucketName}/{request.Key}");
        if (GetObjectMetadataException is not null)
        {
            throw GetObjectMetadataException;
        }

        return Task.FromResult(new GetObjectMetadataResponse());
    }

    public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"ListObjectsV2:{request.BucketName}/{request.Prefix}/{request.ContinuationToken}");
        if (ReturnEmptyListResponse)
        {
            return Task.FromResult(new ListObjectsV2Response { IsTruncated = false });
        }

        return Task.FromResult(request.ContinuationToken is null
            ? new ListObjectsV2Response
            {
                IsTruncated = true,
                NextContinuationToken = "next",
                S3Objects = [new S3Object { Key = "media/one.jpg", LastModified = ListObjectLastModified }]
            }
            : new ListObjectsV2Response
            {
                IsTruncated = false,
                S3Objects = [new S3Object { Key = "media/two.jpg", LastModified = ListObjectLastModified }]
            });
    }

    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
    {
        Operations.Add($"DeleteObject:{request.BucketName}/{request.Key}");
        return Task.FromResult(new DeleteObjectResponse());
    }
}
