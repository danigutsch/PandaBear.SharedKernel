using System.Globalization;
using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.Extensions.Options;
using PandaBear.InputNormalization;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Stores objects in an S3-compatible bucket.
/// </summary>
public sealed class S3ObjectStore(
    IAmazonS3 client,
    IOptions<S3ObjectStoreOptions> storageOptions,
    TimeProvider timeProvider) : IObjectStore
{
    private const string Separator = "/";
    private readonly S3ObjectStoreOptions options = storageOptions.Value;
    private readonly Lock bucketInitializationLock = new();
    private Task? bucketInitialization;

    /// <inheritdoc />
    public async ValueTask<ObjectWriteResult> Put(ObjectWriteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.ObjectKey);
        await EnsureBucketProvisioned(ct).ConfigureAwait(false);

        var putRequest = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = request.ObjectKey,
            InputStream = request.Content,
            ContentType = request.ContentType,
            Headers = { ContentLength = request.Length }
        };
        if (request.Metadata is not null)
        {
            foreach (var (key, value) in request.Metadata)
            {
                putRequest.Metadata[key] = value;
            }
        }

        if (request.Checksum is not null)
        {
            putRequest.Metadata["checksum"] = request.Checksum;
        }

        await client.PutObjectAsync(putRequest, ct).ConfigureAwait(false);

        return new ObjectWriteResult(request.ObjectKey, GetPrivateUri(request.ObjectKey), GetPublicUri(request.ObjectKey), request.ContentType, request.Length, request.Checksum);
    }

    /// <inheritdoc />
    public async ValueTask<ObjectReadResult> OpenRead(string objectKey, CancellationToken ct)
    {
        ValidateKey(objectKey);
        await EnsureBucketProvisioned(ct).ConfigureAwait(false);

        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = options.Bucket, Key = objectKey }, ct).ConfigureAwait(false);
            var checksum = GetMetadataValue(response.Metadata, "checksum");
            return new ObjectReadResult(objectKey, new S3ObjectResponseStream(response), response.Headers.ContentType, response.Headers.ContentLength, checksum);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The object is unavailable.", objectKey, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> Exists(string objectKey, CancellationToken ct)
    {
        ValidateKey(objectKey);
        await EnsureBucketProvisioned(ct).ConfigureAwait(false);

        try
        {
            _ = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = options.Bucket, Key = objectKey }, ct).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListKeys(string prefix, CancellationToken ct)
    {
        var objects = await List(prefix, ct).ConfigureAwait(false);
        return objects.Select(static item => item.Key).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ObjectInventoryItem>> ListObjects(string prefix, CancellationToken ct)
    {
        var objects = await List(prefix, ct).ConfigureAwait(false);
        return objects.Select(static item => new ObjectInventoryItem(item.Key, ToUtcDateTimeOffset(item.LastModified.GetValueOrDefault()))).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<ObjectUploadTicket> CreateUploadUrl(ObjectUploadRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ValidateKey(request.ObjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContentType);
        if (request.ContentType.Contains('\r', StringComparison.Ordinal) || request.ContentType.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Content type must not contain newlines.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Length);
        if (request.ExpiresAfter <= TimeSpan.Zero || request.ExpiresAfter > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Upload lifetime must be positive and at most seven days.");
        }

        var expiresAt = timeProvider.GetUtcNow().Add(request.ExpiresAfter);
        var signingRequest = new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = request.ObjectKey,
            Verb = HttpVerb.PUT,
            Protocol = options.Endpoint?.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS,
            ContentType = request.ContentType,
            Expires = expiresAt.UtcDateTime
        };
        signingRequest.Headers.ContentLength = request.Length;
        var uri = new Uri(await client.GetPreSignedURLAsync(signingRequest).ConfigureAwait(false));
        IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = request.ContentType,
            ["Content-Length"] = request.Length.ToString(CultureInfo.InvariantCulture)
        };
        return new ObjectUploadTicket(request.ObjectKey, uri, expiresAt, headers);
    }

    /// <inheritdoc />
    public Uri GetPublicUri(string objectKey)
    {
        ArgumentNullException.ThrowIfNull(objectKey);
        ValidateKey(objectKey);
        var baseUriKind = options.PublicBaseUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
        var baseUri = options.PublicBaseUri.OriginalString.EndsWith(Separator, StringComparison.Ordinal)
            ? options.PublicBaseUri
            : new Uri(options.PublicBaseUri.OriginalString + Separator, baseUriKind);
        var escapedKey = string.Join(Separator, objectKey.Split(Separator).Select(Uri.EscapeDataString));
        return baseUri.IsAbsoluteUri ? new Uri(baseUri, escapedKey) : new Uri(baseUri.OriginalString + escapedKey, UriKind.Relative);
    }

    /// <inheritdoc />
    public async ValueTask Delete(string objectKey, CancellationToken ct)
    {
        ValidateKey(objectKey);
        await EnsureBucketProvisioned(ct).ConfigureAwait(false);
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = options.Bucket, Key = objectKey }, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<S3Object>> List(string prefix, CancellationToken ct)
    {
        ValidatePrefix(prefix);
        await EnsureBucketProvisioned(ct).ConfigureAwait(false);
        var objects = new List<S3Object>();
        string? continuationToken = null;
        do
        {
            var response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = options.Bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, ct).ConfigureAwait(false);
            objects.AddRange(response.S3Objects ?? []);
            continuationToken = response.IsTruncated.GetValueOrDefault() ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return objects;
    }

    private Uri GetPrivateUri(string objectKey)
    {
        var endpoint = options.Endpoint ?? throw new InvalidOperationException("S3 endpoint must be configured.");
        var baseUri = endpoint.OriginalString.EndsWith(Separator, StringComparison.Ordinal)
            ? endpoint
            : new Uri(endpoint.OriginalString + Separator, UriKind.Absolute);
        var escapedKey = string.Join(Separator, objectKey.Split(Separator).Select(Uri.EscapeDataString));
        return new Uri(baseUri, $"{options.Bucket}/{escapedKey}");
    }

    private static string? GetMetadataValue(MetadataCollection metadata, string key)
    {
        if (metadata.Keys.Contains(key, StringComparer.Ordinal))
        {
            return metadata[key];
        }

        var headerKey = $"x-amz-meta-{key}";
        return metadata.Keys.Contains(headerKey, StringComparer.Ordinal) ? metadata[headerKey] : null;
    }

    private static DateTimeOffset ToUtcDateTimeOffset(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return new DateTimeOffset(utc);
    }

    private async Task EnsureBucketProvisioned(CancellationToken ct)
    {
        if (!options.AutoProvisionBucket)
        {
            return;
        }

        Task initialization;
        lock (bucketInitializationLock)
        {
            initialization = bucketInitialization ??= EnsureBucketCore();
        }

        try
        {
            await initialization.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            if (initialization.IsFaulted || initialization.IsCanceled)
            {
                lock (bucketInitializationLock)
                {
                    if (ReferenceEquals(bucketInitialization, initialization))
                    {
                        bucketInitialization = null;
                    }
                }
            }

            throw;
        }
    }

    private async Task EnsureBucketCore()
    {
        using var timeout = new CancellationTokenSource(options.BucketProvisioningTimeout);
        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = options.Bucket }, timeout.Token).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (string.Equals(exception.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.Ordinal))
        {
            // A concurrent process created the application-owned bucket first.
        }
    }

    private void ValidateKey(string objectKey)
    {
        if (!ObjectStorageKeyValidator.IsValidRelativeKey(objectKey, options.MaxObjectKeyLength))
        {
            throw new ArgumentException("Object key must be a relative slash-delimited path without dot segments.", nameof(objectKey));
        }
    }

    private void ValidatePrefix(string prefix)
    {
        if (!string.IsNullOrEmpty(prefix))
        {
            ValidateKey(prefix.TrimEnd('/'));
        }
    }
}
