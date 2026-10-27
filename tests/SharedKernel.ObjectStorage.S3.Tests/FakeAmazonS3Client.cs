using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Records S3 operations and returns canned responses so the S3 object store can be tested without a server.</summary>
internal sealed class FakeAmazonS3Client : AmazonS3Client
{
    /// <summary>Creates a client with anonymous credentials that points at an unused local endpoint.</summary>
    public FakeAmazonS3Client()
        : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost:8333", ForcePathStyle = true })
    {
    }

    /// <summary>Gets the operations the store invoked, in call order.</summary>
    public List<string> Operations { get; } = [];

    /// <summary>Gets whether OpenTelemetry instrumentation was suppressed during the last metadata lookup, or null before any lookup.</summary>
    public bool? SuppressedDuringMetadataLookup
    {
        get; private set;
    }

    /// <summary>Gets the most recent upload request, or null before any upload.</summary>
    public PutObjectRequest? LastPutObjectRequest
    {
        get; private set;
    }

    /// <summary>Gets or sets the task a bucket creation waits on, or null to complete immediately.</summary>
    public Task<PutBucketResponse>? PutBucketTask
    {
        get; set;
    }

    /// <summary>Gets or sets the exception a metadata lookup throws, or null to succeed.</summary>
    public Exception? GetObjectMetadataException
    {
        get; set;
    }

    /// <summary>Gets or sets the exception an object read throws, or null to succeed.</summary>
    public Exception? GetObjectException
    {
        get; set;
    }

    /// <summary>Gets or sets the last-modified time reported for listed objects.</summary>
    public DateTime ListObjectLastModified { get; set; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    /// <summary>Gets or sets whether listing returns no objects.</summary>
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
        SuppressedDuringMetadataLookup = OpenTelemetry.Sdk.SuppressInstrumentation;
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
